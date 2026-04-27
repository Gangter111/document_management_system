using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using DocumentManagement.Contracts.Documents;
using DocumentManagement.Wpf.Commands;
using DocumentManagement.Wpf.Services;

namespace DocumentManagement.Wpf.ViewModels;

public class DocumentDetailViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    private DocumentDto? _document;

    public DocumentDto? Document
    {
        get => _document;
        set => SetProperty(ref _document, value);
    }

    public ObservableCollection<DocumentAttachmentViewModel> Attachments { get; } = new();

    public ICommand OpenAttachmentCommand { get; }

    public DocumentDetailViewModel(ApiService apiService)
    {
        _apiService = apiService;
        OpenAttachmentCommand = new RelayCommand(OpenAttachment);
    }

    public async Task LoadAsync(long id)
    {
        Document = await _apiService.GetDocumentByIdAsync(id);

        Attachments.Clear();

        // Client-server chuẩn: attachment phải lấy qua API.
        // Tạm để rỗng, không gọi service local/backend trong WPF.
    }

    private static void OpenAttachment(object? parameter)
    {
        if (parameter is not DocumentAttachmentViewModel attachment)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(attachment.StoredFilePath))
        {
            return;
        }

        if (!File.Exists(attachment.StoredFilePath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = attachment.StoredFilePath,
            UseShellExecute = true
        });
    }
}

public class DocumentAttachmentViewModel
{
    public long Id { get; set; }

    public long DocumentId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string StoredFilePath { get; set; } = string.Empty;
}