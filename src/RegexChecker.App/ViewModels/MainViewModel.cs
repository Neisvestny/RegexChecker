using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RegexChecker.Core;
using RegexChecker.Core.Evaluation;

namespace RegexChecker.App.ViewModels;

public enum StatusLevel
{
    Neutral,
    Info,
    Success,
    Warning,
    Error,
}

public partial class MainViewModel : ViewModelBase
{
    private List<MatchInfo> _matches = [];
    private int _currentMatch;

    [ObservableProperty]
    private string expressionInput = "";

    [ObservableProperty]
    private string textInput = "";

    [ObservableProperty]
    private string resultText = "";

    [ObservableProperty]
    private string statusMessage = "Введите выражение и текст, затем нажмите «Проверить».";

    [ObservableProperty]
    private string matchSummary = "";

    [ObservableProperty]
    private int selectionStart;

    [ObservableProperty]
    private int selectionEnd;

    [ObservableProperty]
    private int errorPosition;

    [ObservableProperty]
    private bool hasMatches;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isSuccess;

    [ObservableProperty]
    private bool isWarning;

    [ObservableProperty]
    private bool isError;

    [ObservableProperty]
    private bool isInfo;

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private async Task Check()
    {
        if (string.IsNullOrEmpty(ExpressionInput))
        {
            ClearHighlight();
            ResultText = "";
            SetStatus(StatusLevel.Warning, "Введите регулярное выражение для проверки.");
            return;
        }

        if (string.IsNullOrEmpty(TextInput))
        {
            ClearHighlight();
            ResultText = "";
            SetStatus(StatusLevel.Warning, "Введите строку, в которой нужно выполнить проверку.");
            return;
        }

        // копии на случай правки во время проверки
        string pattern = ExpressionInput;
        string text = TextInput;

        IsBusy = true;
        try
        {
            SearchOutcome outcome = await Task.Run(() => RegexHelper.Search(pattern, text));
            ApplyOutcome(pattern, outcome, text);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanCheck() => !IsBusy;

    partial void OnIsBusyChanged(bool value) => CheckCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void Clear()
    {
        ExpressionInput = "";
        TextInput = "";
        ResultText = "";
        ErrorPosition = 0;
        ClearHighlight();
        SetStatus(StatusLevel.Neutral, "Введите выражение и текст, затем нажмите «Проверить».");
    }

    [RelayCommand]
    private void NextMatch()
    {
        if (_matches.Count == 0)
            return;

        _currentMatch = (_currentMatch + 1) % _matches.Count;
        ApplyCurrentSelection();
    }

    [RelayCommand]
    private void PrevMatch()
    {
        if (_matches.Count == 0)
            return;

        _currentMatch = (_currentMatch - 1 + _matches.Count) % _matches.Count;
        ApplyCurrentSelection();
    }

    private void ApplyOutcome(string pattern, SearchOutcome outcome, string text)
    {
        switch (outcome)
        {
            case SearchInvalidExpression invalid:
                ClearHighlight();
                ResultText = "";
                ErrorPosition = invalid.Error.Position;
                SetStatus(
                    StatusLevel.Warning,
                    $"«{pattern}» не является регулярным выражением: " +
                    $"{invalid.Error.Message} (позиция {invalid.Error.Position}).");
                break;

            case SearchFailed failed:
                ClearHighlight();
                ResultText = "";
                SetStatus(StatusLevel.Error, failed.Message);
                break;

            case SearchSuccess success:
                ApplyMatches(text, success.Matches);
                break;
        }
    }

    private void ApplyMatches(string text, IReadOnlyList<MatchInfo> found)
    {
        _matches = found.ToList(); // только непустые совпадения
        _currentMatch = 0;

        if (_matches.Count == 0)
        {
            ClearHighlight();
            ResultText = "";
            SetStatus(StatusLevel.Info, "Регулярное выражение не найдено в строке.");
            return;
        }

        ResultText = text;
        HasMatches = true;
        ApplyCurrentSelection();
        SetStatus(StatusLevel.Success, $"Найдено вхождений: {_matches.Count}.");
    }

    private void ApplyCurrentSelection()
    {
        var match = _matches[_currentMatch];
        SelectionStart = match.Start;
        SelectionEnd = match.End;
        MatchSummary =
            $"Вхождение {_currentMatch + 1} из {_matches.Count}: " +
            $"позиция {match.Start}, длина {match.Length}";
    }

    private void ClearHighlight()
    {
        _matches = [];
        _currentMatch = 0;
        HasMatches = false;
        MatchSummary = "";
        SelectionStart = 0;
        SelectionEnd = 0;
    }

    private void SetStatus(StatusLevel level, string message)
    {
        IsSuccess = level == StatusLevel.Success;
        IsWarning = level == StatusLevel.Warning;
        IsError = level == StatusLevel.Error;
        IsInfo = level == StatusLevel.Info;
        StatusMessage = message;
    }

    partial void OnExpressionInputChanged(string value) =>
        InvalidateResult("Выражение изменено — нажмите «Проверить».");

    partial void OnTextInputChanged(string value) =>
        InvalidateResult("Строка изменена — нажмите «Проверить».");

    private void InvalidateResult(string message)
    {
        if (ResultText.Length == 0 && _matches.Count == 0)
            return; // результата нет — статус не трогаем

        ClearHighlight();
        ResultText = "";
        SetStatus(StatusLevel.Neutral, message);
    }
}
