﻿using Raylib_cs;
using System.Numerics;
using System.Text;
using static Raylib_cs.Raylib;

public class PracticeScene : Scene
{
    const string Tag = "PracticeScene";

    CourseLesson lesson;
    PloverServer server;
    WPM wpm;
    Paper paper;

    // Words
    string targetText;
    List<Word> words;
    int currentWordIndex;

    // Smooth scrolling
    float yOffset;
    float prevY = -1;
    
    // Progress bar
    float smoothProgressPercent = 0;
    int totalWordCount = 0;

    // Fonts
    Font primaryFont;
    Font secondaryFont;
    Font smallFont;
    int primaryCharWidth = 0;
    
    // Timer
    bool timerRunning = false;
    float timer = 0;
    float timeSinceType = 0;
    const float StopTimingThreshold = 5f;

    // Console
    DebugConsole console;

    // Keyboard
    KeyboardDisplay keyboard;

    public PracticeScene(CourseLesson lesson, PloverServer server, DebugConsole console, Paper paper, KeyboardDisplay keyboard)
    {
        this.lesson = lesson;
        this.server = server;
        this.paper = paper ?? new();
        this.console = console ?? new();
        this.keyboard = keyboard ?? new();
        wpm = new();
        words = new();

        targetText = lesson.Prompts ?? "";
        // TODO Do something else for strict spaces
        var targetWords = targetText.Split(" ", StringSplitOptions.RemoveEmptyEntries);
        words = targetWords.Select(w => new Word {
            Target = w
        }).ToList();

        Input.OnTextTyped += OnTextTyped;
        Input.OnBackspace += OnBackspace;
    }

    public void Load() 
    {
        SetFontSize(40);
    }

    public void Unload() 
    {
        Input.OnTextTyped -= OnTextTyped;
        Input.OnBackspace -= OnBackspace;
    }
    
    public void Draw() 
    {
        const float padding = 10f;

        ClearBackground(Shared.BackgroundColor);

        int leftPanelWidth = Math.Max(primaryCharWidth * 8 + (int)padding * 2, paper.Width);
        int mainPanelWidth = GetScreenWidth() - leftPanelWidth * 2;

        DrawWords(leftPanelWidth, mainPanelWidth);
        
        Vector2 origin = new Vector2(leftPanelWidth + padding, GetScreenHeight() / 2 - primaryFont.BaseSize);
        Vector2 cursor = origin;

        // Draw left panel
        DrawRectangle(0, 0, leftPanelWidth, GetScreenHeight() + 1, Shared.PanelColor);
        
        // Draw paper
        cursor.X = 0;
        cursor.Y = 0;
        paper.Draw(cursor);

        keyboard.Draw(new Vector2(0, 0), leftPanelWidth);

        Color timerColor = Shared.TextColor;
        if (timerRunning)
        {
            timerColor = Shared.AltTextColor;
        }
        
        // Draw WPM
        cursor.X = padding;
        cursor.Y = GetScreenHeight() - primaryFont.BaseSize - padding * 3;
        Util.DrawText(primaryFont, FormatWpm(wpm.GetWPM()), cursor, timerColor);

        // Draw timer
        string timerText = FormatTime(timer);
        cursor.X = padding;
        cursor.Y -= primaryFont.BaseSize + padding;
        Util.DrawText(primaryFont, timerText, cursor, timerColor);
        
        // Draw progress bar
        const int progressBarHeight = 20;
        float progressPercent = (float)currentWordIndex / words.Count;
        smoothProgressPercent = Util.ExpDecay(smoothProgressPercent, progressPercent, Shared.SlideSpeed, GetFrameTime());
        int progressPixels = (int)(GetScreenWidth() * smoothProgressPercent);
        DrawRectangle(0, GetScreenHeight() - progressBarHeight, progressPixels, progressBarHeight  + 1, Shared.AltTextColor);

        console.Draw();
    }

    void DrawWords(int leftMost, int width)
    {
        if (words.Count == 0) return;

        const int vPadding = 10;
        const int hPadding = 160;
        
        int rightMost = width - hPadding;
        leftMost += hPadding;

        // Calculate word positions ahead of time, relative to first word
        // so that we can center the current word 

        List<(Vector2 pos, string topWord, string bottomWord)> wordInfo = [];

        Vector2 cursor = new();
        int fontWidth = Util.GetTextWidth(" ", primaryFont);
        
        // Since we might add words due to splitting input, we'll need to recalculate the current index
        int currentWordIndex = this.currentWordIndex;

        foreach (var word in words)
        {
            string trimmedInput = word.InputBuffer.ToString().Trim();
            string[] subInput = trimmedInput.Split(' ');
            for (int i = 0; i < subInput.Length; i++)
            {
                string iWord = subInput[i];
                if (i > 0) currentWordIndex++;

                string targetWord = (i == 0) ? word.Target : "";
                int targetWidth = Util.GetTextWidth(targetWord, primaryFont);

                int inputWidth = Util.GetTextWidth(iWord, primaryFont);
                int textWidth = Math.Max(targetWidth, inputWidth) + fontWidth;

                if (cursor.X + textWidth > width - hPadding * 2)
                {
                    cursor.Y += (primaryFont.BaseSize + vPadding) * 2;
                    cursor.X = 0;
                }
                wordInfo.Add((cursor, iWord, targetWord));

                cursor.X += textWidth;
            }
        }

        // Adjust positions so that current word is centered, and words are aligned within block
        int yCenter = GetScreenHeight() / 2 - primaryFont.BaseSize * 2;
        Vector2 offset = new(leftMost + hPadding, yCenter - wordInfo[currentWordIndex].pos.Y);

        float rawY = offset.Y;
        if (Shared.UserSettings.SmoothScroll)
        {
            if (offset.Y != prevY && prevY != -1)
            {
                yOffset = offset.Y - prevY;
            }
            prevY = rawY;
            offset.Y -= yOffset;
        }

        for (int i = 0; i <  wordInfo.Count; i++)
        {
            var info = wordInfo[i];
            wordInfo[i] = (info.pos + offset, info.topWord, info.bottomWord);
        }
        
        // Draw words
        foreach (var info in wordInfo)
        {
            Vector2 pos = info.pos;

            Color inputWordColor = Shared.AltTextColor;
            if (info.topWord.Trim() != info.bottomWord)
            {
                inputWordColor = Shared.ErrTextColor;
                Vector2 underlinePos = new(pos.X, pos.Y + primaryFont.BaseSize * .9f);
                Vector2 underlineSize = new(Util.GetTextWidth(info.topWord, primaryFont), primaryFont.BaseSize * .05f);
                Util.DrawRectangle(underlinePos, underlineSize, Color.Red);
            }

            Util.DrawText(primaryFont, info.topWord, pos, inputWordColor);

            pos.Y += primaryFont.BaseSize;
            Util.DrawText(primaryFont, info.bottomWord, pos, Shared.TextColor);
        }
    }

    void TextChanged()
    {
        // Why is this method so cursed?

        timeSinceType = 0;

        var word = words[currentWordIndex];
        var inputWord = word.InputBuffer.ToString();
        if (inputWord.Trim() == word.Target)
        {
            AdvanceWord();
            Log.Trace(Tag, $"Now on word {currentWordIndex}: {words[currentWordIndex].Target}");
        }
        else if (currentWordIndex < words.Count - 1)
        {
            var inputWords = inputWord.Split(" ");
            var nextWord = words[currentWordIndex + 1].Target;
            
            // Check if part of the input matches the next word
            // We only check the second word onward
            for (int i = 1; i < inputWords.Length; i++)
            {
                // If the first "word" is just a space, don't consider it
                if (i == 1 && inputWords[0].Trim() == "") continue;
                if (inputWords[i] == nextWord)
                {
                    // Split the input into two, where the second part contains the new word
                    int splitPoint = inputWord.IndexOfInstance(' ', i);
                    if (splitPoint == 0) return;

                    string prevPart = inputWord.Substring(0, splitPoint);
                    string nextPart = inputWord.Substring(splitPoint);

                    Log.Trace(Tag, $"Found valid next word, splitting input buffer. prevPart: \"{prevPart}\" nextPart: \"{nextPart}\"");
                    word.InputBuffer = new(prevPart);

                    AdvanceWord();
                    words[currentWordIndex].InputBuffer = new(nextPart);

                    TextChanged();
                    break;
                }
            }
        }
    }

    void AdvanceWord()
    {
        currentWordIndex = Math.Min(currentWordIndex + 1, words.Count - 1);

        for (int i = 0; i < currentWordIndex - 1; i++)
        {
            var word = words[i];
            if (word.InputBuffer.ToString().Trim() != word.Target)
            {
                word.SoftError = true;
            }
        }
    }
    void OnBackspace(int count)
    {
        var buffer = words[currentWordIndex].InputBuffer;
        while (count > 0)
        {
            if (buffer.Length < count)
            {
                count -= buffer.Length;
                buffer.Length = 0;

                if (currentWordIndex == 0)
                {
                    return;
                }

                currentWordIndex -= 1;
                buffer = words[currentWordIndex].InputBuffer;
            }
            else
            {
                buffer.Length -= count;
                count = 0;
            }
        }

        TextChanged();
    }

    void OnTextTyped(string text)
    {
        var word = words[currentWordIndex];
        word.Add(text);
        
        TextChanged();
    }

    void SetFontSize(int fontSize)
    {
        primaryFont = Shared.GetFont(Shared.PrimaryFontFile, fontSize);
        secondaryFont = Shared.GetFont(Shared.SecondaryFontFile, fontSize);
        smallFont = Shared.GetFont(Shared.PrimaryFontFile, (int)(fontSize * .5f));

        // Assuming mono-spaced font, so character width will be consistent
        primaryCharWidth = Util.GetTextWidth(" ", primaryFont);

        paper.SetFont(smallFont);
    }

    string FormatTime(float time)
    {
        int minutes = (int)(time / 60);
        int seconds = (int)(time % 60);

        return $"{minutes,3:D1}:{seconds:D2}";
    }

    string FormatWpm(int wpm)
    {
        if (wpm > 999)
        {
            wpm = 999;
        }
        return $"{wpm,3} WPM";
    }

    public void Update()
    {
        server.DispatchMessages();

        yOffset = Util.ExpDecay(yOffset, 0, Shared.SlideSpeed, GetFrameTime());

        timeSinceType += GetFrameTime();
        if (timeSinceType > StopTimingThreshold)
        {
            timerRunning = false;
        }
    }

    class Word
    {
        public string Target = "";
        public StringBuilder InputBuffer = new();
        
        // Could be incorrectly flagged due to a word taking multiple strokes
        // one of which translated to backspaces
        public bool SoftError = false;
        
        // User manually backspaced in the word to correct
        public bool Backspaced = false;

        public bool AnyError => SoftError || Backspaced;

        public void Add(string text)
        {
            InputBuffer.Append(text);
        }

        public bool Backspace(int count)
        {
            if (count > InputBuffer.Length)
            {
                InputBuffer.Length = 0;
                return false;
            }
            
            InputBuffer.Length -= count;
            return true;
        }
    }
}