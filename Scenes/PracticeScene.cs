﻿using Raylib_cs;
using System.Numerics;
using System.Text;
using static Raylib_cs.Raylib;

public class PracticeScene : Scene
{
    const string Tag = "PracticeScene";

    Font primaryFont = Shared.GetFont(Shared.PrimaryFontFile, 80);
    Font secondaryFont = Shared.GetFont(Shared.SecondaryFontFile, 80);
    string lessonPath;
    List<Word> words;

    bool complete = false;
    float timeSinceComplete = 0f;

    int wordIndex = 0;
    float xOffset = 0; // Used for horizontal smooth scrolling
    float smoothProgressPercent = 0;

    bool timerRunning = false;
    float timer = 0;
    float timeSinceType = 0;
    const float StopTimingThreshold = 5f;


    public PracticeScene(string lessonFilepath)
    {
        lessonPath = lessonFilepath;
        words = new();
    }

    public void Load() 
    {
        var lesson = Lesson.Load(lessonPath);
        if (lesson is null)
        {
            Log.Error(Tag, "Unable to load lesson scene");
            throw new ApplicationException($"Unable to load lesson {lessonPath}", null);
        }
        words = lesson.Words ?? [];
    }

    public void Unload() { }
    
    public void Draw() 
    {
        Vector2 origin = new Vector2(GetScreenWidth() / 2, GetScreenHeight() / 2 - primaryFont.BaseSize);
        Vector2 cursor = origin;
        cursor.X += xOffset;

        // Draw cursor
        Word word = words[wordIndex];
        DrawRectangle((int)cursor.X + GetWordWidth(word, true), (int)cursor.Y, 2, primaryFont.BaseSize, Shared.AccentColor);
        
        // Draw words
        for (int i = wordIndex; i < words.Count; i++)
        {
            word = words[i];
            int width = DrawWord(word, cursor, false);
            cursor.X += width;

            if (cursor.X > GetScreenWidth()) break;
        }

        cursor = origin;
        cursor.X += xOffset;
        for (int i = wordIndex - 1; i >= 0 && i < words.Count; i--)
        {
            word = words[i];
            cursor.X -= GetWordWidth(word);
            DrawWord(word, cursor, true);

            if (cursor.X < 0) break;
        }

        // Draw progress bar
        const int progressBarHeight = 20;
        float progressPercent = (float)wordIndex / words.Count;
        if (complete)
        {
            // If we are done, set progress to 100%
            progressPercent = 1;
        }
        smoothProgressPercent = ExpDecay(smoothProgressPercent, progressPercent, 15, GetFrameTime());
        int progressPixels = (int)(GetScreenWidth() * smoothProgressPercent);
        DrawRectangle(0, GetScreenHeight() - progressBarHeight, progressPixels, progressBarHeight  + 1, Shared.AltTextColor);

        // Draw timer
        Color timerColor = Shared.TextColor;
        if (timerRunning)
        {
            timerColor = Shared.AltTextColor;
        }
        string timerText = FormatTime(timer);
        float timerX = (GetScreenWidth() - GetTextWidth(timerText)) / 2;
        float timerY = (int)(GetScreenHeight() * .5 - primaryFont.BaseSize * 3f);
        DrawTextEx(secondaryFont, timerText, new Vector2(timerX, timerY), secondaryFont.BaseSize, 0, timerColor);

        ClearBackground(Shared.BackgroundColor);
    }

    public void Update() 
    {
        timeSinceType += GetFrameTime();
        
        float dy = GetMouseWheelMoveV().Y;
        if (dy != 0)
        {
            int newFontSize = primaryFont.BaseSize + (int)dy;
            newFontSize = Math.Clamp(newFontSize, 20, 120);

            primaryFont = Shared.GetFont(Shared.PrimaryFontFile, newFontSize);
            secondaryFont = Shared.GetFont(Shared.SecondaryFontFile, newFontSize);
        }

        int key = GetCharPressed();
        while (key > 0)
        {
            if (!complete)
            {
                timerRunning = true;
                timeSinceType = 0;

                if (key == ' ')
                {
                    if (words[wordIndex].InputBuffer.Length > 0)
                    {
                        if (Shared.UserSettings.SmoothScroll && wordIndex < words.Count - 1)
                        {
                            xOffset += GetWordWidth(words[wordIndex], false);
                        }
                    
                        wordIndex = Math.Min(wordIndex + 1, words.Count - 1);
                    }
                }
                else
                {
                    words[wordIndex].InputBuffer.Append((char)key);
                
                    // Check if we are done
                    if (wordIndex == words.Count - 1 && words[wordIndex].InputBuffer.ToString() == words[wordIndex].Text)
                    {
                        complete = true;
                        timerRunning = false;
                    }
                }
            }
            key = GetCharPressed();
        }

        key = GetKeyPressed();
        while (key > 0)
        {
            if (!complete)
            {
                timeSinceType = 0;
                if (key == (int)KeyboardKey.Backspace)
                {
                    Backspace();
                }
            }
            key = GetKeyPressed();
        }

        if (IsKeyPressedRepeat(KeyboardKey.Backspace) && !complete)
        {
            timeSinceType = 0;
            Backspace();
        }

        xOffset = ExpDecay(xOffset, 0, 20, GetFrameTime());

        if (timeSinceType > StopTimingThreshold)
        {
            timerRunning = false;
        }

        if (timerRunning)
        {
            timer += GetFrameTime();
        }
    }

    float ExpDecay(float a, float b, float decay, float dt)
    {
        return b + (a - b) * MathF.Exp(-decay * dt);
    }

    void Backspace()
    {
        var word = words[wordIndex];
        if (word.InputBuffer.Length == 0)
        {
            // Go to previous word
            if (wordIndex > 0)
            {
                wordIndex--;
                if (Shared.UserSettings.SmoothScroll)
                {
                    xOffset -= GetWordWidth(words[wordIndex], false);
                }
            }
        }
        else
        {
            // Remove last character
            StringBuilder newString = new();
            newString.Append(word.InputBuffer.ToString().Substring(0, word.InputBuffer.Length - 1));

            word.InputBuffer = newString;
        }
    }

    /// <summary>
    /// Get pixel width of word, including space
    /// </summary>
    /// <param name="word">The word to measure</param>
    /// <param name="truncate">only consider the number of characters in the input buffer</param>
    /// <returns>pixel width of word</returns>
    int GetWordWidth(Word word, bool truncate = false)
    {
        string text = word.Text;
        var inputBuffer = word.InputBuffer;
        if (inputBuffer.Length > word.Text.Length)
        {
            var extraInput = inputBuffer.ToString().Substring(word.Text.Length);
            text += extraInput;
        }

        if (truncate)
        {
            return GetTextWidth(text.Substring(0, word.InputBuffer.Length));
        }
        return GetTextWidth(text + " ");
    }

    void DrawInputBuffer(Word word, Vector2 pos)
    {
        DrawTextEx(primaryFont, word.InputBuffer.ToString(), pos, primaryFont.BaseSize, 0, Shared.AltTextColor);
    }

    int DrawWord(Word word, Vector2 pos, bool visited)
    {
        string text = word.Text;
        StringBuilder buffer = word.InputBuffer;

        DrawInputBuffer(word, pos);
        pos.Y += primaryFont.BaseSize;

        int totalWidth = 0;

        for (int i = 0; i < Math.Max(text.Length, buffer.Length); i++)
        {
            bool textContains = text.Length > i;
            bool bufferContains = buffer.Length > i;
            float fontSize = primaryFont.BaseSize;

            string str = "";
            Color color = Color.Black;

            if (textContains && !bufferContains)
            {
                str = text[i].ToString();
                color = Shared.TextColor;
            }
            else if (bufferContains && !textContains)
            {
                str = buffer[i].ToString();
                color = Shared.AltErrTextColor;
            }
            else if (buffer[i] != text[i])
            {
                str = text[i].ToString();
                color = Shared.ErrTextColor;
            }
            else // Match
            {
                str = buffer[i].ToString();
                color = Shared.AltTextColor;
            }
            DrawTextEx(primaryFont, str, pos, fontSize, 0, color);

            int width = GetTextWidth(str);
            pos.X += width;
            totalWidth += width;
        }

        if (visited && word.Text != word.InputBuffer.ToString())
        {
            // Move cursor back to the start of the word, and down
            pos.X -= totalWidth;
            pos.Y += primaryFont.BaseSize;

            DrawRectangle((int)pos.X, (int)pos.Y, totalWidth, 5, Shared.ErrTextColor);
        }

        return totalWidth + GetTextWidth(" ");
    }
    
    string FormatTime(float time)
    {
        int minutes = (int)(time / 60);
        int seconds = (int)(time % 60);

        return $"{minutes:D1}:{seconds:D2}";
    }

    unsafe int GetTextWidth(string text)
    {
        float width = 0;

        for (int i = 0; i < text.Length; i++)
        {
            int codepointByteCount = 0;
            int codepoint = GetCodepoint(text[i].ToString(), ref codepointByteCount);
            int index = GetGlyphIndex(primaryFont, codepoint);

            if (primaryFont.Glyphs[index].AdvanceX == 0)
            {
                width += primaryFont.Recs[index].Width;
            }
            else
            {
                width += primaryFont.Glyphs[index].AdvanceX;
            }
        }

        return (int)width;
    }
}