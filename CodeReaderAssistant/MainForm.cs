using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace CodeReaderAssistant;

public sealed class MainForm : Form
{
    private const int WmClipboardUpdate = 0x031D;
    private static readonly Color WindowColor = Color.FromArgb(22, 26, 35);
    private static readonly Color SurfaceColor = Color.FromArgb(31, 37, 49);
    private static readonly Color InputColor = Color.FromArgb(40, 47, 61);
    private static readonly Color AccentColor = Color.FromArgb(78, 121, 211);
    private static readonly Color AccentHoverColor = Color.FromArgb(96, 142, 235);
    private static readonly Color PrimaryTextColor = Color.FromArgb(239, 243, 250);
    private static readonly Color SecondaryTextColor = Color.FromArgb(169, 181, 202);

    private readonly ComboBox modeCombo = new();
    private readonly ComboBox languageCombo = new();
    private readonly TextBox selectedTextBox = new();
    private readonly RichTextBox resultBox = new();
    private readonly Label languageLabel = new();
    private readonly Label statusLabel = new();
    private readonly CheckBox topMostCheckBox = new();
    private readonly CheckBox autoProcessCheckBox = new();
    private readonly TextBox notesBox = new();
    private readonly System.Windows.Forms.Timer selectionTimer = new();
    private readonly MemoryStore memory = new();
    private Button refreshButton = new();
    private CancellationTokenSource? activeRequest;
    private string lastSelectionSignature = string.Empty;

    public MainForm()
    {
        Text = "Code Reader Assistant";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(780, 720);
        Size = new Size(1040, 860);
        TopMost = true;
        BackColor = WindowColor;
        ForeColor = PrimaryTextColor;
        Font = new Font("Segoe UI", 10.5F);

        BuildUi();
        UpdateModeUi();

        selectionTimer.Interval = 300;
        selectionTimer.Tick += (_, _) => CheckForSelectedText();
        selectionTimer.Start();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AddClipboardFormatListener(Handle);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        selectionTimer.Stop();
        activeRequest?.Cancel();
        RemoveClipboardFormatListener(Handle);
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmClipboardUpdate)
        {
            CheckForSelectedText();
        }

        base.WndProc(ref m);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Padding = new Padding(24),
            BackColor = BackColor,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        Controls.Add(root);

        var titlePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceColor,
            Padding = new Padding(16, 8, 0, 0),
        };
        var title = new Label
        {
            Text = "Code Reader Assistant",
            Font = new Font("Segoe UI Semibold", 22F),
            ForeColor = PrimaryTextColor,
            AutoSize = true,
            Location = new Point(16, 6),
        };
        var subtitle = new Label
        {
            Text = "Локальный перевод и объяснение кода с автоматическим чтением выделения",
            Font = new Font("Segoe UI", 10F),
            ForeColor = SecondaryTextColor,
            AutoSize = true,
            Location = new Point(18, 45),
        };
        titlePanel.Controls.Add(title);
        titlePanel.Controls.Add(subtitle);
        root.Controls.Add(titlePanel, 0, 0);

        var selectors = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            BackColor = BackColor,
        };
        selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
        selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        selectors.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        selectors.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        selectors.Controls.Add(MakeLabel("Режим"), 0, 0);
        languageLabel.Text = "Язык";
        languageLabel.ForeColor = Color.FromArgb(168, 178, 195);
        languageLabel.AutoSize = true;
        selectors.Controls.Add(languageLabel, 2, 0);

        ConfigureCombo(modeCombo, "Переводчик", "Программирование");
        modeCombo.SelectedIndexChanged += (_, _) => UpdateModeUi();
        selectors.Controls.Add(modeCombo, 0, 1);
        selectors.SetColumnSpan(modeCombo, 2);

        ConfigureCombo(languageCombo, "Python", "C++");
        selectors.Controls.Add(languageCombo, 2, 1);
        selectors.SetColumnSpan(languageCombo, 2);
        root.Controls.Add(selectors, 0, 1);

        var settings = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = SurfaceColor,
            Padding = new Padding(14, 0, 0, 0),
        };
        var localStatusLabel = MakeLabel("●  Локальный режим  ·  без ключей и регистрации");
        localStatusLabel.Dock = DockStyle.None;
        localStatusLabel.AutoSize = false;
        localStatusLabel.Width = 620;
        localStatusLabel.Height = 34;
        localStatusLabel.Margin = new Padding(0, 4, 0, 0);
        localStatusLabel.ForeColor = Color.FromArgb(145, 211, 172);
        settings.Controls.Add(localStatusLabel);
        root.Controls.Add(settings, 0, 2);

        var selectedPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = BackColor,
        };
        selectedPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        selectedPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        selectedPanel.Controls.Add(MakeLabel("Выделенный текст"), 0, 0);
        selectedTextBox.Multiline = true;
        selectedTextBox.ScrollBars = ScrollBars.Vertical;
        selectedTextBox.Dock = DockStyle.Fill;
        selectedTextBox.BackColor = InputColor;
        selectedTextBox.ForeColor = PrimaryTextColor;
        selectedTextBox.BorderStyle = BorderStyle.FixedSingle;
        selectedTextBox.Font = new Font("Consolas", 11F);
        selectedTextBox.AcceptsTab = true;
        selectedPanel.Controls.Add(selectedTextBox, 0, 1);
        root.Controls.Add(selectedPanel, 0, 3);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = BackColor,
        };
        var processButton = MakeButton("Обработать сейчас", 160, AccentColor);
        processButton.Click += async (_, _) => await ProcessSelectionAsync(selectedTextBox.Text);
        refreshButton = MakeButton("Пересчитать", 125, Color.FromArgb(62, 82, 125));
        refreshButton.Enabled = false;
        refreshButton.Click += async (_, _) => await ProcessSelectionAsync(selectedTextBox.Text, true);
        var pasteButton = MakeButton("Взять из буфера", 145, Color.FromArgb(62, 82, 125));
        pasteButton.Click += (_, _) => PasteClipboardText();

        autoProcessCheckBox.Text = "Обрабатывать сразу при выделении";
        autoProcessCheckBox.Checked = true;
        autoProcessCheckBox.AutoSize = true;
        autoProcessCheckBox.Margin = new Padding(16, 10, 0, 0);
        autoProcessCheckBox.ForeColor = PrimaryTextColor;

        topMostCheckBox.Text = "Поверх окон";
        topMostCheckBox.Checked = true;
        topMostCheckBox.AutoSize = true;
        topMostCheckBox.Margin = new Padding(20, 10, 0, 0);
        topMostCheckBox.ForeColor = PrimaryTextColor;
        topMostCheckBox.CheckedChanged += (_, _) => TopMost = topMostCheckBox.Checked;

        actions.Controls.Add(processButton);
        actions.Controls.Add(refreshButton);
        actions.Controls.Add(pasteButton);
        actions.Controls.Add(autoProcessCheckBox);
        actions.Controls.Add(topMostCheckBox);
        root.Controls.Add(actions, 0, 4);

        var resultPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = BackColor,
        };
        resultPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        resultPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        resultPanel.Controls.Add(MakeLabel("Результат"), 0, 0);
        resultBox.Dock = DockStyle.Fill;
        resultBox.ReadOnly = true;
        resultBox.BackColor = InputColor;
        resultBox.ForeColor = PrimaryTextColor;
        resultBox.BorderStyle = BorderStyle.FixedSingle;
        resultBox.Font = new Font("Segoe UI", 14F);
        resultBox.DetectUrls = false;
        resultBox.ScrollBars = RichTextBoxScrollBars.Vertical;
        resultPanel.Controls.Add(resultBox, 0, 1);
        root.Controls.Add(resultPanel, 0, 5);

        var notesPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = BackColor,
        };
        notesPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        notesPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var notesHeader = new Panel { Dock = DockStyle.Fill };
        var notesLabel = MakeLabel("Моя заметка");
        notesLabel.Width = 160;
        notesLabel.Dock = DockStyle.Left;
        var saveNoteButton = MakeButton("Сохранить заметку", 155, Color.FromArgb(62, 82, 125));
        saveNoteButton.Dock = DockStyle.Right;
        saveNoteButton.Margin = new Padding(0, 0, 0, 0);
        saveNoteButton.Click += (_, _) => SaveCurrentNote();
        notesHeader.Controls.Add(notesLabel);
        notesHeader.Controls.Add(saveNoteButton);
        notesPanel.Controls.Add(notesHeader, 0, 0);
        notesBox.Multiline = true;
        notesBox.ScrollBars = ScrollBars.Vertical;
        notesBox.Dock = DockStyle.Fill;
        notesBox.BackColor = InputColor;
        notesBox.ForeColor = PrimaryTextColor;
        notesBox.BorderStyle = BorderStyle.FixedSingle;
        notesBox.Font = new Font("Segoe UI", 11F);
        notesPanel.Controls.Add(notesBox, 0, 1);
        root.Controls.Add(notesPanel, 0, 6);

        statusLabel.ForeColor = SecondaryTextColor;
        statusLabel.AutoEllipsis = true;
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(statusLabel, 0, 7);
        statusLabel.Text = "Выделите текст в редакторе — приложение обработает его автоматически.";
    }

    private void UpdateModeUi()
    {
        var programming = modeCombo.SelectedIndex == 1;
        languageCombo.Enabled = programming;
        languageLabel.ForeColor = programming
            ? Color.FromArgb(168, 178, 195)
            : Color.FromArgb(90, 98, 112);

        if (selectedTextBox is not null && !string.IsNullOrWhiteSpace(selectedTextBox.Text))
        {
            _ = ProcessSelectionAsync(selectedTextBox.Text);
        }
    }

    private async Task ProcessSelectionAsync(string text, bool forceRefresh = false)
    {
        var value = text.Trim();
        if (value.Length == 0)
        {
            return;
        }

        var mode = modeCombo.SelectedIndex == 1 ? AppMode.Programming : AppMode.Translator;
        var language = languageCombo.SelectedIndex == 1 ? ProgrammingLanguage.Cpp : ProgrammingLanguage.Python;
        var cached = memory.Find(value, mode, language);
        if (!forceRefresh && cached is { Result.Length: > 0 })
        {
            resultBox.Text = cached.Result;
            notesBox.Text = cached.Note;
            refreshButton.Enabled = true;
            statusLabel.Text = "Готово из памяти. Нажмите «Пересчитать», чтобы получить новый разбор.";
            statusLabel.ForeColor = Color.FromArgb(145, 158, 178);
            return;
        }

        activeRequest?.Cancel();
        activeRequest?.Dispose();
        var requestCancellation = new CancellationTokenSource();
        activeRequest = requestCancellation;

        resultBox.Text = "Обрабатываю выделение...";
        statusLabel.Text = "Использую локальную справку и переводчик.";
        statusLabel.ForeColor = Color.FromArgb(145, 158, 178);
        refreshButton.Enabled = false;

        try
        {
            await Task.Yield();
            requestCancellation.Token.ThrowIfCancellationRequested();
            var result = mode == AppMode.Translator
                ? LocalAssistant.Translate(value)
                : LocalAssistant.Analyze(value, language);

            if (!requestCancellation.IsCancellationRequested)
            {
                resultBox.Text = result;
                memory.SaveResult(value, mode, language, result);
                refreshButton.Enabled = true;
                statusLabel.Text = "Готово. Ответ сохранён в локальную память.";
            }
        }
        catch (OperationCanceledException)
        {
            // A newer selection replaced this request.
        }
        catch (Exception exception)
        {
            if (!requestCancellation.IsCancellationRequested)
            {
                resultBox.Text = $"Не удалось обработать выделение.\r\n\r\n{exception.Message}";
                statusLabel.Text = "Не удалось обработать выделение.";
                statusLabel.ForeColor = Color.FromArgb(255, 140, 140);
            }
        }
        finally
        {
            if (ReferenceEquals(activeRequest, requestCancellation))
            {
                activeRequest = null;
            }

            requestCancellation.Dispose();
        }
    }

    private void CheckForSelectedText()
    {
        if (!autoProcessCheckBox.Checked || !IsHandleCreated)
        {
            return;
        }

        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero || IsOurWindow(foregroundWindow))
        {
            return;
        }

        var selected = TryReadSelectedText();
        if (string.IsNullOrWhiteSpace(selected))
        {
            return;
        }

        var signature = $"{foregroundWindow}:{selected}";
        if (signature == lastSelectionSignature)
        {
            return;
        }

        lastSelectionSignature = signature;
        selectedTextBox.Text = selected;
        _ = ProcessSelectionAsync(selected);
    }

    private static string TryReadSelectedText()
    {
        try
        {
            var element = AutomationElement.FocusedElement;
            for (var depth = 0; element is not null && depth < 6; depth++)
            {
                if (element.TryGetCurrentPattern(TextPattern.Pattern, out var pattern)
                    && pattern is TextPattern textPattern)
                {
                    var ranges = textPattern.GetSelection();
                    var selected = string.Join(Environment.NewLine, ranges.Select(range => range.GetText(-1)));
                    if (!string.IsNullOrWhiteSpace(selected))
                    {
                        return selected.Trim();
                    }
                }

                element = TreeWalker.ControlViewWalker.GetParent(element);
            }
        }
        catch (ElementNotAvailableException)
        {
            // The editor changed focus while the selection was being read.
        }
        catch (COMException)
        {
            // Some editors do not expose their text selection through UI Automation.
        }
        catch (InvalidOperationException)
        {
            // The focused element can disappear during a selection change.
        }

        return string.Empty;
    }

    private void SaveCurrentNote()
    {
        var value = selectedTextBox.Text.Trim();
        if (value.Length == 0)
        {
            statusLabel.Text = "Сначала выберите или вставьте текст, к которому нужно добавить заметку.";
            return;
        }

        var mode = modeCombo.SelectedIndex == 1 ? AppMode.Programming : AppMode.Translator;
        var language = languageCombo.SelectedIndex == 1 ? ProgrammingLanguage.Cpp : ProgrammingLanguage.Python;
        memory.SaveNote(value, mode, language, notesBox.Text);
        statusLabel.Text = "Заметка сохранена локально.";
        statusLabel.ForeColor = Color.FromArgb(145, 158, 178);
    }

    private void PasteClipboardText()
    {
        try
        {
            if (!Clipboard.ContainsText())
            {
                statusLabel.Text = "В буфере обмена нет текста.";
                return;
            }

            var text = Clipboard.GetText();
            selectedTextBox.Text = text;
            lastSelectionSignature = string.Empty;
            _ = ProcessSelectionAsync(text);
        }
        catch (ExternalException)
        {
            statusLabel.Text = "Буфер обмена занят другой программой. Попробуйте ещё раз.";
        }
    }

    private bool IsOurWindow(IntPtr windowHandle)
    {
        GetWindowThreadProcessId(windowHandle, out var processId);
        return processId == (uint)Environment.ProcessId;
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text,
        ForeColor = SecondaryTextColor,
        Font = new Font("Segoe UI Semibold", 10F),
        AutoSize = true,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static void ConfigureCombo(ComboBox combo, params string[] values)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.FlatStyle = FlatStyle.Flat;
        combo.BackColor = InputColor;
        combo.ForeColor = PrimaryTextColor;
        combo.Font = new Font("Segoe UI", 11F);
        combo.Dock = DockStyle.Fill;
        combo.Items.AddRange(values);
        combo.SelectedIndex = 0;
    }

    private static Button MakeButton(string text, int width, Color backgroundColor) => new()
    {
        Text = text,
        Width = width,
        Height = 38,
        FlatStyle = FlatStyle.Flat,
        BackColor = backgroundColor,
        ForeColor = Color.White,
        FlatAppearance = { BorderSize = 0, MouseOverBackColor = AccentHoverColor },
        Margin = new Padding(0, 4, 8, 0),
        Cursor = Cursors.Hand,
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
