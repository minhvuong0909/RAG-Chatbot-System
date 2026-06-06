using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Helpers;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Models;
using RagChatbotSystem.DataAccess.Repositories;

namespace RagChatbotSystem.Business.Services
{
    public class UserService : IUserService
    {
        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<User> _userRepository;
        private readonly IEmailService _emailService;

        public UserService(IUnitOfWork unitOfWork, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _userRepository = _unitOfWork.Repository<User>();
            _emailService = emailService;
        }

        public async Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
        {
            return await _userRepository.GetQueryable()
                .AsNoTracking()
                .OrderBy(u => u.FullName)
                .Select(u => new UserDto(
                    u.UserId,
                    u.FullName,
                    u.Email,
                    u.Username,
                    u.Role,
                    u.CreatedAt,
                    u.IsApproved,
                    u.MustChangePassword))
                .ToListAsync(cancellationToken);
        }

        public async Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _userRepository.GetQueryable()
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new UserDto(
                    u.UserId,
                    u.FullName,
                    u.Email,
                    u.Username,
                    u.Role,
                    u.CreatedAt,
                    u.IsApproved,
                    u.MustChangePassword))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
            {
                throw new ArgumentException("Full name is required.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                throw new ArgumentException("Email is required.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                throw new ArgumentException("Password is required.", nameof(request));
            }

            var email = NormalizeEmail(request.Email);
            await EnsureEmailAvailableAsync(email, cancellationToken);

            var roleName = string.IsNullOrWhiteSpace(request.Role) ? "Student" : request.Role.Trim();
            var user = new User
            {
                UserId = Guid.NewGuid(),
                FullName = request.FullName.Trim(),
                Email = email,
                Username = await GenerateUniqueUsernameFromEmailAsync(email, cancellationToken),
                PasswordHash = PasswordHasherHelper.HashPassword(request.Password),
                Role = roleName,
                CreatedAt = DateTime.UtcNow,
                IsApproved = true,
                MustChangePassword = false,
                LastPasswordChangedAt = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ToDto(user);
        }

        public async Task<UserDto?> AuthenticateUserAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return null;
            }

            var identifier = email.Trim().ToLowerInvariant();
            var user = await _userRepository.GetQueryable()
                .FirstOrDefaultAsync(u => u.Email.ToLower() == identifier || u.Username.ToLower() == identifier, cancellationToken);

            if (user == null)
            {
                return null;
            }

            if (!PasswordHasherHelper.VerifyPassword(password, user.PasswordHash))
            {
                return null;
            }

            if (!user.IsApproved)
            {
                throw new InvalidOperationException("Tai khoan cua ban dang bi khoa hoac chua duoc phe duyet.");
            }

            user.LastLoginAt = DateTime.UtcNow;
            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ToDto(user);
        }

        public async Task<bool> ApproveUserAsync(Guid userId, bool approve, CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetQueryable()
                .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (user == null)
            {
                return false;
            }

            user.IsApproved = approve;
            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<ProvisionedAccountDto> CreateTeacherByAdminAsync(AdminCreateTeacherRequest request, Guid adminUserId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
            {
                throw new ArgumentException("Teacher full name is required.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                throw new ArgumentException("Teacher email is required.", nameof(request));
            }

            if (request.DatasetIds == null || request.DatasetIds.Count == 0)
            {
                throw new ArgumentException("At least one subject must be assigned to the teacher.", nameof(request));
            }

            var email = NormalizeEmail(request.Email);
            await EnsureEmailAvailableAsync(email, cancellationToken);

            var datasetRepo = _unitOfWork.Repository<Dataset>();
            var assignmentRepo = _unitOfWork.Repository<TeacherSubjectAssignment>();
            var requestedDatasetIds = request.DatasetIds.Distinct().ToList();

            var foundDatasetIds = await datasetRepo.GetQueryable()
                .Where(d => requestedDatasetIds.Contains(d.DatasetId))
                .Select(d => d.DatasetId)
                .ToListAsync(cancellationToken);

            if (foundDatasetIds.Count != requestedDatasetIds.Count)
            {
                throw new InvalidOperationException("One or more selected subjects were not found.");
            }

            var occupiedSubject = await assignmentRepo.GetQueryable()
                .Where(a => requestedDatasetIds.Contains(a.DatasetId))
                .Select(a => a.Dataset.Name)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(occupiedSubject))
            {
                throw new InvalidOperationException($"Subject '{occupiedSubject}' is already assigned to another teacher.");
            }

            var temporaryPassword = GenerateTemporaryPassword();
            var now = DateTime.UtcNow;
            var user = new User
            {
                UserId = Guid.NewGuid(),
                FullName = request.FullName.Trim(),
                Email = email,
                Username = await GenerateUniqueUsernameFromEmailAsync(email, cancellationToken),
                PasswordHash = PasswordHasherHelper.HashPassword(temporaryPassword),
                Role = "Teacher",
                CreatedAt = now,
                IsApproved = true,
                MustChangePassword = true,
                TemporaryPasswordExpiresAt = now.AddDays(14),
                CreatedByAdminId = adminUserId
            };

            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                await _userRepository.AddAsync(user, cancellationToken);

                foreach (var datasetId in requestedDatasetIds)
                {
                    await assignmentRepo.AddAsync(new TeacherSubjectAssignment
                    {
                        AssignmentId = Guid.NewGuid(),
                        DatasetId = datasetId,
                        TeacherId = user.UserId,
                        AssignedBy = adminUserId,
                        AssignedAt = now
                    }, cancellationToken);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _emailService.SendAccountCreatedEmailAsync(user.Email, user.FullName, user.Username, temporaryPassword, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTracker();
                throw;
            }

            return new ProvisionedAccountDto(user.UserId, user.FullName, user.Email, user.Username, user.Role, temporaryPassword);
        }

        public async Task<StudentImportResult> ImportStudentsFromXlsxAsync(Stream xlsxStream, Guid adminUserId, CancellationToken cancellationToken = default)
        {
            using var workbook = new XLWorkbook(xlsxStream);
            var worksheet = workbook.Worksheets.First();
            var rows = worksheet.RangeUsed()?.RowsUsed().ToList() ?? new List<IXLRangeRow>();
            var results = new List<StudentImportRowResult>();

            if (rows.Count <= 1)
            {
                return new StudentImportResult(0, 0, 0, results);
            }

            var header = rows[0].Cells().Select(c => c.GetString().Trim().ToLowerInvariant()).ToList();
            var emailIndex = FindColumn(header, "email");
            var nameIndex = FindColumn(header, "fullname", "full name", "name", "studentname", "student name");

            if (emailIndex < 0)
            {
                throw new InvalidOperationException("The XLSX file must contain an Email column.");
            }

            var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Skip(1))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rowNumber = row.RowNumber();
                var rawEmail = row.Cell(emailIndex + 1).GetString().Trim();
                var fullName = nameIndex >= 0 ? row.Cell(nameIndex + 1).GetString().Trim() : null;

                try
                {
                    if (string.IsNullOrWhiteSpace(rawEmail))
                    {
                        throw new InvalidOperationException("Email is required.");
                    }

                    var email = NormalizeEmail(rawEmail);
                    if (!seenEmails.Add(email))
                    {
                        throw new InvalidOperationException("Duplicate email in import file.");
                    }

                    await EnsureEmailAvailableAsync(email, cancellationToken);

                    var temporaryPassword = GenerateTemporaryPassword();
                    var username = await GenerateUniqueUsernameFromEmailAsync(email, cancellationToken);
                    var now = DateTime.UtcNow;
                    var user = new User
                    {
                        UserId = Guid.NewGuid(),
                        FullName = string.IsNullOrWhiteSpace(fullName) ? username : fullName,
                        Email = email,
                        Username = username,
                        PasswordHash = PasswordHasherHelper.HashPassword(temporaryPassword),
                        Role = "Student",
                        CreatedAt = now,
                        IsApproved = true,
                        MustChangePassword = true,
                        TemporaryPasswordExpiresAt = now.AddDays(14),
                        CreatedByAdminId = adminUserId
                    };

                    await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
                    try
                    {
                        await _userRepository.AddAsync(user, cancellationToken);
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                        await _emailService.SendAccountCreatedEmailAsync(user.Email, user.FullName, user.Username, temporaryPassword, cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                    }
                    catch
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        _unitOfWork.ClearTracker();
                        throw;
                    }

                    results.Add(new StudentImportRowResult(rowNumber, email, user.FullName, true, username, null));
                }
                catch (Exception ex)
                {
                    results.Add(new StudentImportRowResult(rowNumber, rawEmail, fullName, false, null, ex.Message));
                }
            }

            var created = results.Count(r => r.Success);
            return new StudentImportResult(results.Count, created, results.Count - created, results);
        }

        public async Task<bool> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
        {
            if (request.NewPassword != request.ConfirmNewPassword)
            {
                throw new ArgumentException("New password and confirmation password do not match.");
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            {
                throw new ArgumentException("New password must be at least 8 characters.");
            }

            var user = await _userRepository.GetQueryable()
                .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken);

            if (user == null)
            {
                return false;
            }

            if (!PasswordHasherHelper.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            {
                throw new InvalidOperationException("Current password is incorrect.");
            }

            user.PasswordHash = PasswordHasherHelper.HashPassword(request.NewPassword);
            user.MustChangePassword = false;
            user.TemporaryPasswordExpiresAt = null;
            user.LastPasswordChangedAt = DateTime.UtcNow;

            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task EnsureEmailAvailableAsync(string email, CancellationToken cancellationToken)
        {
            if (!EmailRegex.IsMatch(email))
            {
                throw new InvalidOperationException("Email format is invalid.");
            }

            var exists = await _userRepository.GetQueryable()
                .AnyAsync(u => u.Email.ToLower() == email, cancellationToken);

            if (exists)
            {
                throw new InvalidOperationException("A user with this email already exists.");
            }
        }

        private async Task<string> GenerateUniqueUsernameFromEmailAsync(string email, CancellationToken cancellationToken)
        {
            var baseUsername = email.Split('@')[0].Trim().ToLowerInvariant();
            baseUsername = Regex.Replace(baseUsername, @"[^a-z0-9._-]", string.Empty);
            if (string.IsNullOrWhiteSpace(baseUsername))
            {
                baseUsername = "user";
            }

            var username = baseUsername;
            var suffix = 2;
            while (await _userRepository.GetQueryable().AnyAsync(u => u.Username.ToLower() == username, cancellationToken))
            {
                username = $"{baseUsername}{suffix}";
                suffix++;
            }

            return username;
        }

        private static string GenerateTemporaryPassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnopqrstuvwxyz";
            const string digits = "23456789";
            const string symbols = "!@$?_-";
            const string all = upper + lower + digits + symbols;

            var chars = new List<char>
            {
                Pick(upper),
                Pick(lower),
                Pick(digits),
                Pick(symbols)
            };

            while (chars.Count < 12)
            {
                chars.Add(Pick(all));
            }

            return new string(chars.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray());
        }

        private static char Pick(string source)
        {
            return source[RandomNumberGenerator.GetInt32(source.Length)];
        }

        private static int FindColumn(IReadOnlyList<string> headers, params string[] names)
        {
            foreach (var name in names)
            {
                for (var i = 0; i < headers.Count; i++)
                {
                    if (headers[i] == name)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static string NormalizeEmail(string email)
        {
            return email.Trim().ToLowerInvariant();
        }

        private static UserDto ToDto(User user)
        {
            return new UserDto(
                user.UserId,
                user.FullName,
                user.Email,
                user.Username,
                user.Role,
                user.CreatedAt,
                user.IsApproved,
                user.MustChangePassword);
        }
    }
}
