using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AntManager.Models;
using AntManager.Services;
using AntManager.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AntManager.ViewModels;

public class ApiTestViewModel : ViewModelBase
{
    private readonly HttpClientService _httpService;
    private readonly ApiTemplateStorageService _templateStorageService;
    private readonly IDialogService _dialogService;
    
    private string _selectedMethod = "GET";
    private string _url = "";
    private string _requestBody = "";
    private string _responseBody = "";
    private string _responseStatus = "";
    private int _responseTime;
    private bool _isLoading;
    private bool _isRequestExpanded = true;
    private bool _isResponseExpanded = true;
    private bool _isUrlFocused = false;
    private string _authKey = "";
    private string _authValue = "";
    private string _templateTitle = "";
    private string _templateDescription = "";
    private ApiRequestTemplate? _selectedTemplate;
    private Guid? _currentTemplateId;

    public ObservableCollection<HeaderItem> Headers { get; set; }
    public ObservableCollection<string> HttpMethods { get; set; }
    public ObservableCollection<ApiRequestTemplate> ApiTemplates { get; }

    public ICommand SendRequestCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand AddHeaderCommand { get; }
    public ICommand RemoveHeaderCommand { get; }
    public ICommand FormatJsonCommand { get; }
    public ICommand ToggleRequestCommand { get; }
    public ICommand ToggleResponseCommand { get; }
    public ICommand SaveTemplateCommand { get; }
    public ICommand NewTemplateCommand { get; }
    public ICommand DeleteTemplateCommand { get; }
    public ICommand ImportTemplatesCommand { get; }
    public ICommand ExportTemplatesCommand { get; }
    public ICommand OpenResponsePopupCommand { get; }
    
    public ApiTestViewModel()
        : this(new HttpClientService(), new ApiTemplateStorageService(), new DialogService())
    {
    }

    public ApiTestViewModel(HttpClientService httpService,
                            ApiTemplateStorageService templateStorageService,
                            IDialogService dialogService)
    {
        _httpService = httpService;
        _templateStorageService = templateStorageService;
        _dialogService = dialogService;

        HttpMethods = new ObservableCollection<string> { "GET", "POST", "PUT", "DELETE", "PATCH" };
        Headers = new ObservableCollection<HeaderItem>();
        ApiTemplates = new ObservableCollection<ApiRequestTemplate>(_templateStorageService.LoadTemplates());

        // 로그인 시 토큰이 있으면 기본 Authorization 헤더 추가
        AddDefaultAuthorizationHeader();

        SendRequestCommand = new RelayCommand(async _ => await SendRequest(), _ => !IsLoading && !string.IsNullOrWhiteSpace(Url));
        ClearCommand = new RelayCommand(_ => Clear());
        AddHeaderCommand = new RelayCommand(_ => AddHeader());
        RemoveHeaderCommand = new RelayCommand(param => RemoveHeader(param as HeaderItem));
        FormatJsonCommand = new RelayCommand(_ => FormatJson());
        ToggleRequestCommand = new RelayCommand(_ => ToggleRequest());
        ToggleResponseCommand = new RelayCommand(_ => ToggleResponse());
        SaveTemplateCommand = new RelayCommand(_ => SaveTemplate());
        NewTemplateCommand = new RelayCommand(_ => PrepareNewTemplate());
        DeleteTemplateCommand = new RelayCommand(_ => DeleteSelectedTemplate(), _ => SelectedTemplate != null);
        ImportTemplatesCommand = new RelayCommand(_ => ImportTemplates());
        ExportTemplatesCommand = new RelayCommand(_ => ExportTemplates(), _ => ApiTemplates.Any());
        OpenResponsePopupCommand = new RelayCommand(_ => OpenResponsePopup(), _ => !string.IsNullOrWhiteSpace(ResponseBody));
    }
    
    public string SelectedMethod
    {
        get => _selectedMethod;
        set => SetProperty(ref _selectedMethod, value);
    }
    
    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }
    
    public string RequestBody
    {
        get => _requestBody;
        set => SetProperty(ref _requestBody, value);
    }
    
    public string ResponseBody
    {
        get => _responseBody;
        set => SetProperty(ref _responseBody, value);
    }
    
    public string ResponseStatus
    {
        get => _responseStatus;
        set => SetProperty(ref _responseStatus, value);
    }
    
    public int ResponseTime
    {
        get => _responseTime;
        set => SetProperty(ref _responseTime, value);
    }
    
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsRequestExpanded
    {
        get => _isRequestExpanded;
        set
        {
            SetProperty(ref _isRequestExpanded, value);
            OnPropertyChanged(nameof(RequestRowHeight));
            OnPropertyChanged(nameof(ResponseRowHeight));
        }
    }

    public bool IsResponseExpanded
    {
        get => _isResponseExpanded;
        set
        {
            SetProperty(ref _isResponseExpanded, value);
            OnPropertyChanged(nameof(RequestRowHeight));
            OnPropertyChanged(nameof(ResponseRowHeight));
        }
    }

    public GridLength RequestRowHeight
    {
        get
        {
            if (!IsRequestExpanded) return new GridLength(96);
            if (!IsResponseExpanded) return new GridLength(2, GridUnitType.Star);
            return new GridLength(1, GridUnitType.Star);
        }
    }

    public GridLength ResponseRowHeight
    {
        get
        {
            if (!IsResponseExpanded) return new GridLength(80);
            if (!IsRequestExpanded) return new GridLength(2, GridUnitType.Star);
            return new GridLength(1, GridUnitType.Star);
        }
    }

    public string AuthKey
    {
        get => _authKey;
        set => SetProperty(ref _authKey, value);
    }

    public string AuthValue
    {
        get => _authValue;
        set => SetProperty(ref _authValue, value);
    }

    public bool IsUrlFocused
    {
        get => _isUrlFocused;
        set => SetProperty(ref _isUrlFocused, value);
    }

    public string TemplateTitle
    {
        get => _templateTitle;
        set => SetProperty(ref _templateTitle, value);
    }

    public string TemplateDescription
    {
        get => _templateDescription;
        set => SetProperty(ref _templateDescription, value);
    }

    public ApiRequestTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetProperty(ref _selectedTemplate, value))
            {
                if (value != null)
                {
                    ApplyTemplate(value);
                }
                else
                {
                    TemplateTitle = string.Empty;
                    TemplateDescription = string.Empty;
                }

                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
    
    private async Task SendRequest()
    {
        try
        {
            IsLoading = true;
            ResponseBody = "";
            ResponseStatus = "";
            
            var headers = Headers.Where(h => !string.IsNullOrWhiteSpace(h.Key))
                                .ToDictionary(h => h.Key, h => h.Value);

            string? authType = null;
            string? authValue = null;

            var startTime = DateTime.Now;
            var response = await _httpService.SendRequestAsync(
                SelectedMethod,
                Url,
                RequestBody,
                headers,
                authType,
                authValue
            );
            var endTime = DateTime.Now;
            
            ResponseTime = (int)(endTime - startTime).TotalMilliseconds;
            
            var content = await response.Content.ReadAsStringAsync();
            ResponseStatus = $"{(int)response.StatusCode} {response.ReasonPhrase}";
            
            // Try to format JSON
            try
            {
                var jsonObject = JToken.Parse(content);
                ResponseBody = jsonObject.ToString(Formatting.Indented);
            }
            catch
            {
                ResponseBody = content;
            }
        }
        catch (Exception ex)
        {
            ResponseStatus = "Error";
            ResponseBody = $"Error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    private void Clear()
    {
        Url = "";
        RequestBody = "";
        ResponseBody = "";
        ResponseStatus = "";
        ResponseTime = 0;
        Headers.Clear();
        SelectedMethod = "GET";
        TemplateTitle = "";
        TemplateDescription = "";
        _currentTemplateId = null;
        SelectedTemplate = null;

        // 초기화 후 기본 Authorization 헤더 다시 추가
        AddDefaultAuthorizationHeader();
    }

    private void AddDefaultAuthorizationHeader()
    {
        var antApiService = AntManager.Services.AntApiService.Instance;
        if (antApiService.IsConnected && !string.IsNullOrEmpty(antApiService.Token))
        {
            // Authorization 헤더가 이미 있는지 확인
            var existingAuth = Headers.FirstOrDefault(h => h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase));

            if (existingAuth == null)
            {
                // 없으면 추가
                Headers.Add(new HeaderItem
                {
                    Key = "Authorization",
                    Value = $"Bearer {antApiService.Token}"
                });
            }
            else
            {
                // 있으면 값만 업데이트
                existingAuth.Value = $"Bearer {antApiService.Token}";
            }
        }
    }

    private void AddHeader()
    {
        Headers.Add(new HeaderItem { Key = "", Value = "" });
    }

    private void RemoveHeader(HeaderItem? item)
    {
        if (item != null)
        {
            Headers.Remove(item);
        }
    }

    private void FormatJson()
    {
        if (string.IsNullOrWhiteSpace(RequestBody)) return;

        try
        {
            var jsonObject = JToken.Parse(RequestBody);
            RequestBody = jsonObject.ToString(Formatting.Indented);
        }
        catch
        {
            // Ignore formatting errors
        }
    }

    private void ApplyTemplate(ApiRequestTemplate template)
    {
        TemplateTitle = template.Title;
        TemplateDescription = template.Description;
        SelectedMethod = string.IsNullOrWhiteSpace(template.Method) ? "GET" : template.Method;
        Url = template.Url;
        RequestBody = template.Body;

        // 현재 선택된 템플릿 ID 저장
        _currentTemplateId = template.Id;

        Headers.Clear();
        if (template.Headers != null)
        {
            var antApiService = AntManager.Services.AntApiService.Instance;
            var currentToken = antApiService.IsConnected && !string.IsNullOrEmpty(antApiService.Token)
                ? $"Bearer {antApiService.Token}"
                : null;

            foreach (var header in template.Headers)
            {
                // Authorization 헤더이고 현재 토큰이 있으면 업데이트
                if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) && currentToken != null)
                {
                    Headers.Add(new HeaderItem
                    {
                        Key = header.Key,
                        Value = currentToken
                    });
                }
                else
                {
                    Headers.Add(new HeaderItem
                    {
                        Key = header.Key,
                        Value = header.Value
                    });
                }
            }
        }
    }

    private ApiRequestTemplate BuildTemplateFromRequest(Guid templateId)
    {
        return new ApiRequestTemplate
        {
            Id = templateId,
            Title = TemplateTitle?.Trim() ?? string.Empty,
            Description = TemplateDescription?.Trim() ?? string.Empty,
            Method = SelectedMethod,
            Url = Url,
            Body = RequestBody,
            Headers = Headers
                .Select(header => new ApiHeaderEntry
                {
                    Key = header.Key,
                    Value = header.Value
                })
                .ToList()
        };
    }

    private void SaveTemplate()
    {
        // 템플릿 제목이 비어있으면 경고
        if (string.IsNullOrWhiteSpace(TemplateTitle))
        {
            Views.CommonDialogWindow.ShowDialog(
                "템플릿 제목을 입력해주세요.",
                "템플릿 저장",
                Views.CommonDialogWindow.DialogType.Warning);
            return;
        }

        // 템플릿 선택되어 있고, 내용이 변경된 경우
        if (_currentTemplateId.HasValue)
        {
            var original = ApiTemplates.FirstOrDefault(t => t.Id == _currentTemplateId.Value);

            if (original != null)
            {
                // 내용이 변경되었는지 확인
                bool isModified = original.Url != Url ||
                                original.Body != RequestBody ||
                                original.Method != SelectedMethod ||
                                original.Title != TemplateTitle ||
                                original.Description != TemplateDescription;

                if (isModified)
                {
                    // 팝업으로 선택지 제공
                    var dialog = new Views.CommonDialogWindow(
                        "기존 템플릿이 수정되었습니다.",
                        "템플릿 저장",
                        Views.CommonDialogWindow.DialogType.Question);

                    // 버튼 텍스트 변경
                    dialog.PrimaryButton.Content = "덮어쓰기";
                    dialog.SecondaryButton.Content = "새로 만들기";

                    var owner = Application.Current?.Windows
                        .OfType<Window>()
                        .FirstOrDefault(w => w.IsActive) ?? Application.Current?.MainWindow;

                    if (owner != null)
                    {
                        dialog.Owner = owner;
                    }

                    dialog.ShowDialog();

                    if (dialog.Result == Views.CommonDialogWindow.CommonDialogResult.Yes)
                    {
                        // 기존 템플릿 덮어쓰기
                        OverwriteTemplate(original);
                    }
                    else if (dialog.Result == Views.CommonDialogWindow.CommonDialogResult.No)
                    {
                        // 새 템플릿으로 저장
                        SaveAsNewTemplate();
                    }
                }
                else
                {
                    // 변경 사항이 없으면 그냥 저장
                    Views.CommonDialogWindow.ShowDialog(
                        "변경 사항이 없습니다.",
                        "템플릿 저장",
                        Views.CommonDialogWindow.DialogType.Info);
                }
                return;
            }
        }

        // 템플릿 선택 안 됨 → 바로 새로 저장
        SaveAsNewTemplate();
    }

    private void OverwriteTemplate(ApiRequestTemplate original)
    {
        // 기존 템플릿의 인덱스 찾기
        var index = ApiTemplates.IndexOf(original);

        // 새로운 템플릿 객체 생성 (같은 ID 유지)
        var updatedTemplate = new ApiRequestTemplate
        {
            Id = original.Id,
            Title = TemplateTitle?.Trim() ?? string.Empty,
            Description = TemplateDescription?.Trim() ?? string.Empty,
            Method = SelectedMethod,
            Url = Url,
            Body = RequestBody,
            Headers = Headers
                .Select(header => new ApiHeaderEntry
                {
                    Key = header.Key,
                    Value = header.Value
                })
                .ToList()
        };

        // ObservableCollection에서 교체하여 UI 업데이트 트리거
        if (index >= 0)
        {
            ApiTemplates[index] = updatedTemplate;
        }

        PersistTemplates();

        // 선택된 템플릿도 업데이트
        SelectedTemplate = updatedTemplate;

        Views.CommonDialogWindow.ShowDialog(
            "기존 템플릿을 덮어썼습니다.",
            "템플릿 저장",
            Views.CommonDialogWindow.DialogType.Info);
    }

    private void SaveAsNewTemplate()
    {
        var newTemplate = new ApiRequestTemplate
        {
            Id = Guid.NewGuid(),
            Title = TemplateTitle.Trim(),
            Description = TemplateDescription?.Trim() ?? string.Empty,
            Method = SelectedMethod,
            Url = Url,
            Body = RequestBody,
            Headers = Headers
                .Select(header => new ApiHeaderEntry
                {
                    Key = header.Key,
                    Value = header.Value
                })
                .ToList()
        };

        ApiTemplates.Add(newTemplate);
        PersistTemplates();
        SelectedTemplate = newTemplate;
        _currentTemplateId = newTemplate.Id;
        Views.CommonDialogWindow.ShowDialog(
            "새 템플릿으로 저장되었습니다.",
            "템플릿 저장",
            Views.CommonDialogWindow.DialogType.Info);
    }

    private void PrepareNewTemplate()
    {
        SelectedTemplate = null;
        TemplateTitle = string.Empty;
        TemplateDescription = string.Empty;
        _currentTemplateId = null;
    }

    private void DeleteSelectedTemplate()
    {
        if (SelectedTemplate == null) return;

        var result = Views.CommonDialogWindow.ShowDialog(
            $"'{SelectedTemplate.Title}' 템플릿을 삭제하시겠습니까?",
            "템플릿 삭제",
            Views.CommonDialogWindow.DialogType.Warning);

        if (!result)
        {
            return;
        }

        ApiTemplates.Remove(SelectedTemplate);
        SelectedTemplate = null;
        TemplateTitle = string.Empty;
        TemplateDescription = string.Empty;
        PersistTemplates();
    }

    private void ImportTemplates()
    {
        var filePath = _dialogService.ShowOpenFileDialog("JSON 파일 (*.json)|*.json", "템플릿 불러오기");
        if (string.IsNullOrWhiteSpace(filePath)) return;

        try
        {
            var imported = _templateStorageService.ImportFromFile(filePath);
            var existingIds = ApiTemplates.Select(t => t.Id).ToHashSet();
            var addedCount = 0;

            foreach (var template in imported)
            {
                if (existingIds.Contains(template.Id))
                {
                    template.Id = Guid.NewGuid();
                }

                ApiTemplates.Add(template);
                existingIds.Add(template.Id);
                addedCount++;
            }

            PersistTemplates();
            Views.CommonDialogWindow.ShowDialog(
                $"템플릿 {addedCount}개를 불러왔습니다.",
                "불러오기 완료",
                Views.CommonDialogWindow.DialogType.Info);
        }
        catch (Exception ex)
        {
            Views.CommonDialogWindow.ShowDialog(
                $"불러오기에 실패했습니다.\n{ex.Message}",
                "불러오기 오류",
                Views.CommonDialogWindow.DialogType.Error);
        }
    }

    private void ExportTemplates()
    {
        if (!ApiTemplates.Any())
        {
            Views.CommonDialogWindow.ShowDialog(
                "내보낼 템플릿이 없습니다.",
                "템플릿 내보내기",
                Views.CommonDialogWindow.DialogType.Warning);
            return;
        }

        var filePath = _dialogService.ShowSaveFileDialog("JSON 파일 (*.json)|*.json", "템플릿 내보내기", "api-templates.json");
        if (string.IsNullOrWhiteSpace(filePath)) return;

        try
        {
            _templateStorageService.ExportToFile(ApiTemplates, filePath);
            Views.CommonDialogWindow.ShowDialog(
                "템플릿을 내보냈습니다.",
                "내보내기 완료",
                Views.CommonDialogWindow.DialogType.Info);
        }
        catch (Exception ex)
        {
            Views.CommonDialogWindow.ShowDialog(
                $"내보내기에 실패했습니다.\n{ex.Message}",
                "내보내기 오류",
                Views.CommonDialogWindow.DialogType.Error);
        }
    }

    private void PersistTemplates()
    {
        _templateStorageService.SaveTemplates(ApiTemplates);
        CommandManager.InvalidateRequerySuggested();
    }

    private void ToggleRequest()
    {
        if (IsRequestExpanded && IsResponseExpanded)
        {
            // 둘 다 열려있으면 요청만 닫기
            IsRequestExpanded = false;
        }
        else if (!IsRequestExpanded && IsResponseExpanded)
        {
            // 요청 닫혀있고 응답 열려있으면 요청 열기
            IsRequestExpanded = true;
        }
        else if (IsRequestExpanded && !IsResponseExpanded)
        {
            // 요청 열려있고 응답 닫혀있으면 응답도 열기
            IsResponseExpanded = true;
        }
        else
        {
            // 둘 다 닫혀있으면 요청만 열기
            IsRequestExpanded = true;
        }
    }

    private void ToggleResponse()
    {
        if (IsRequestExpanded && IsResponseExpanded)
        {
            // 둘 다 열려있으면 응답만 닫기
            IsResponseExpanded = false;
        }
        else if (IsRequestExpanded && !IsResponseExpanded)
        {
            // 응답 닫혀있고 요청 열려있으면 응답 열기
            IsResponseExpanded = true;
        }
        else if (!IsRequestExpanded && IsResponseExpanded)
        {
            // 응답 열려있고 요청 닫혀있으면 요청도 열기
            IsRequestExpanded = true;
        }
        else
        {
            // 둘 다 닫혀있으면 응답만 열기
            IsResponseExpanded = true;
        }
    }

    private void OpenResponsePopup()
    {
        var popup = new Views.ResponsePopupWindow(ResponseBody, ResponseStatus, ResponseTime);

        var owner = Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive) ?? Application.Current?.MainWindow;

        if (owner != null)
        {
            popup.Owner = owner;
        }

        popup.ShowDialog();
    }
}

public class HeaderItem : ViewModelBase
{
    private string _key = "";
    private string _value = "";
    
    public string Key
    {
        get => _key;
        set => SetProperty(ref _key, value);
    }
    
    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}
