using DocumentManagement.Intelligence.Adapters;
using DocumentManagement.Intelligence.Confidence;
using DocumentManagement.Intelligence.Extractors;
using DocumentManagement.Intelligence.Normalization;
using DocumentManagement.Intelligence.Results;

namespace DocumentManagement.Intelligence;

public sealed class SemanticExtractionPipeline
{
    private readonly MinerUContentListV2Adapter _adapter;
    private readonly IssuerExtractor _issuerExtractor;
    private readonly DocumentNumberExtractor _documentNumberExtractor;
    private readonly IssueDateExtractor _issueDateExtractor;
    private readonly TitleExtractor _titleExtractor;
    private readonly SignerExtractor _signerExtractor;
    private readonly RecipientExtractor _recipientExtractor;
    private readonly BodyExtractor _bodyExtractor;
    private readonly ExtractionDebugReportGenerator _debugReportGenerator = new();

    public SemanticExtractionPipeline()
        : this(new VietnameseTextNormalizer())
    {
    }

    public SemanticExtractionPipeline(VietnameseTextNormalizer normalizer)
    {
        _adapter = new MinerUContentListV2Adapter(normalizer);
        _issuerExtractor = new IssuerExtractor(normalizer);
        _documentNumberExtractor = new DocumentNumberExtractor(normalizer);
        _issueDateExtractor = new IssueDateExtractor(normalizer);
        _titleExtractor = new TitleExtractor(normalizer);
        _signerExtractor = new SignerExtractor(normalizer);
        _recipientExtractor = new RecipientExtractor(normalizer);
        _bodyExtractor = new BodyExtractor(normalizer);
    }

    public DocumentExtractionResult ExtractFromMinerUContentListV2(string json)
    {
        var structure = _adapter.Read(json);
        var result = new DocumentExtractionResult(
            _issuerExtractor.Extract(structure),
            _documentNumberExtractor.Extract(structure),
            _issueDateExtractor.Extract(structure),
            _titleExtractor.Extract(structure),
            _signerExtractor.Extract(structure),
            _recipientExtractor.Extract(structure),
            _bodyExtractor.Extract(structure));

        return result with
        {
            DebugReport = _debugReportGenerator.Generate(result),
            PageMetrics = structure.Pages
        };
    }
}
