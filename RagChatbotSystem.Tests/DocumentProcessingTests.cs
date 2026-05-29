using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using RagChatbotSystem.Business.Services;

namespace RagChatbotSystem.Tests;

public class DocumentProcessingTests
{
    [Fact]
    public void SplitTextSegments_OverlapsAdjacentChunks()
    {
        var text = string.Join(" ", Enumerable.Range(0, 80).Select(i => $"word{i:D2}"));

        var chunks = DocumentService.SplitTextSegments(
            new[] { new ExtractedTextSegment(text, 1) },
            chunkSize: 120,
            chunkOverlap: 30);

        Assert.True(chunks.Count > 1);
        Assert.Contains(chunks[0].Content[^30..].Trim(), chunks[1].Content);
    }

    [Fact]
    public void SplitTextSegments_UsesDominantPageWhenChunkCrossesPages()
    {
        var pageOne = new string('A', 20);
        var pageTwo = new string('B', 120);

        var chunks = DocumentService.SplitTextSegments(
            new[]
            {
                new ExtractedTextSegment(pageOne, 1),
                new ExtractedTextSegment(pageTwo, 2)
            },
            chunkSize: 80,
            chunkOverlap: 20);

        Assert.Equal(2, chunks[0].PageNumber);
    }

    [Fact]
    public async Task ExtractTextSegmentsAsync_ReadsTxtAsPageOne()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("hello from txt"));

        var segments = await DocumentService.ExtractTextSegmentsAsync(stream, "txt");

        var segment = Assert.Single(segments);
        Assert.Equal(1, segment.PageNumber);
        Assert.Equal("hello from txt", segment.Text);
    }

    [Fact]
    public async Task ExtractTextSegmentsAsync_ReadsDocxAsPageOne()
    {
        await using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(new Paragraph(new Run(new Text("hello from docx")))));
            mainPart.Document.Save();
        }

        stream.Position = 0;

        var segments = await DocumentService.ExtractTextSegmentsAsync(stream, "docx");

        var segment = Assert.Single(segments);
        Assert.Equal(1, segment.PageNumber);
        Assert.Contains("hello from docx", segment.Text);
    }

    [Fact]
    public async Task ExtractTextSegmentsAsync_RejectsUnsupportedFileType()
    {
        await using var stream = new MemoryStream();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            DocumentService.ExtractTextSegmentsAsync(stream, "png"));
    }
}
