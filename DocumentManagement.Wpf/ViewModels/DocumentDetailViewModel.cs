using DocumentManagement.Application.Interfaces;
using DocumentManagement.Domain.Entities;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DocumentManagement.Wpf.Commands;
using System.Diagnostics;
using System.IO;

namespace DocumentManagement.Wpf.ViewModels;

public class DocumentDetailViewModel : BaseViewModel
{
    private readonly IDocumentService _documentService;
    private readonly IAttachmentService _attachmentService;

    private Document? _document;
    public Document? Document { get => _document; set => SetProperty(ref _document, value); }

    public ObservableCollection<DocumentAttachment> Attachments { get; } = new();

    public ICommand OpenAttachmentCommand { get; }

    public DocumentDetailViewModel(IDocumentService documentService, IAttachmentService attachmentService)
    {
        _documentService = documentService;
        _attachmentService = attachmentService;
        OpenAttachmentCommand = new RelayCommand(async p => await OpenAttachmentAsync(p));
    }

    public async Task LoadAsync(long id)
    {
        Document = await _documentService.GetByIdAsync(id);
        var files = await _attachmentService.GetByDocumentIdAsync(id);
        Attachments.Clear();
        foreach (var file in files) Attachments.Add(file);
    }

    private async Task OpenAttachmentAsync(object? parameter)
    {
        if (parameter is DocumentAttachment att && File.Exists(att.StoredFilePath))
        {
            using Process? _ = Process.Start(new ProcessStartInfo(fileName: att.StoredFilePath) { UseShellExecute = true });
        }
    }
}
