﻿using ImGuiNET;
using Raylib_cs;
using System.Numerics;

public class DebugConsole
{
    const string Tag = "DebugConsole";
    const int MaxLines = 1000;

    CircularBuffer<DebugMessage> messages;
    Font font;
    int charWidth;
    //bool scrollToBottom;
    bool[] severityToggles = [false, false, false, false, false, false];
    
    bool[] tagToggles;
    float maxTagWidth;
    List<string> tags;
    Dictionary<string, int> tagToIndex;

    public DebugConsole()
    {
        messages = new(MaxLines);
        tags = new();
        tagToggles = Array.Empty<bool>();
        tagToIndex = new();
        Log.OnLogMessage += HandleLogMessage;
    }

    public void SetFont(Font font)
    {
        this.font = font;
        charWidth = Util.GetTextWidth(" ", font);
    }
    public void Draw()
    {
        ImGui.Begin("Debug Console");

        for (int i = 1; i < severityToggles.Length; i++)
        {
            ImGui.Checkbox(Log.SeverityTag[i], ref severityToggles[i]);
            if (i < severityToggles.Length - 1)
            {
                ImGui.SameLine();
            }
        }

        ImGui.Separator();

        var style = ImGui.GetStyle();

        float frameMaxTagWidth = 0;
        float windowWidth = ImGui.GetCursorPos().X + ImGui.GetContentRegionAvail().X;
        float x = 0;
        for (int i = 0; i < tags.Count; i++)
        {
            ImGui.Checkbox(tags[i], ref tagToggles[i]);
            float buttonWidth = ImGui.GetItemRectSize().X;
            maxTagWidth = Math.Max(maxTagWidth, buttonWidth);
            frameMaxTagWidth = Math.Max(frameMaxTagWidth, buttonWidth);
            x += buttonWidth + style.ItemSpacing.X;
            
            float nextButtonX = x + maxTagWidth + style.ItemSpacing.X;
            
            if (i < tags.Count - 1 && nextButtonX < windowWidth)
            {
                ImGui.SameLine();
            }
            else
            {
                x = 0;
            }
        }
        // If the window was resized, the maxTagWidth is incorrect
        if (frameMaxTagWidth < maxTagWidth)
        {
            maxTagWidth = frameMaxTagWidth;
        }

        ImGui.Separator();

        ImGui.BeginChild("Debug messages");
        foreach (var line in messages)
        {
            if (line.Text is null) continue;

            if (severityToggles.Any(x => x) && !severityToggles[(int)line.Severity])
            {
                continue;
            }

            if (tagToggles.Any(x => x) && !tagToggles[line.TagIndex])
            {
                continue;
            }

            Vector4 color;
            
            ImGui.PushStyleColor(ImGuiCol.Text, GetLineColor(line.Severity));
            ImGui.TextWrapped(line.Text);
            ImGui.PopStyleColor();

            // This is supposed to scroll to the bottom, but it scrolls to the top??
            // the log message order is inverted so it works as expected 
            //if (scrollToBottom)
            //{
            //    ImGui.SetScrollHereY(1.0f);
            //    scrollToBottom = false;
            //}
        }
        ImGui.EndChild();
        ImGui.End();
    }

    Vector4 GetLineColor(Severity sev)
    {
        //Vector4 color;
        Color color;
        if (sev == Severity.Warning)
        {
            color = Shared.WarningTextColor;
        }
        else if (sev == Severity.Error || sev == Severity.Warning)
        {
            color = Shared.ErrTextColor;
        }
        else unsafe
        {
            return *ImGui.GetStyleColorVec4(ImGuiCol.Text);
        }

        return new Vector4(color.R, color.G, color.B, color.A) / 255f;
    }

    void HandleLogMessage(Severity sev, string tag, string message, bool ignoreFilter)
    {
        // If there is a new tag, add it to:
        //  tag list
        //  tag toggle list
        //  tag to index lookup
        if (!tagToIndex.ContainsKey(tag))
        {
            tagToIndex.Add(tag, tags.Count);
            tags.Add(tag);
            tagToggles = [..tagToggles, false];
        }
        
        messages.Add(new DebugMessage 
        {
            Severity = sev,
            TagIndex = tagToIndex[tag],
            Text = message
        });

        //scrollToBottom = true;
    }

    struct DebugMessage
    {
        public string? Text { get; set; }
        public Severity Severity { get; set; }
        public int TagIndex { get; set; }
    }

    // For testing purposes
    void RandomMessages(float chance)
    {
        var rnd = new Random();
        if (rnd.NextDouble() < chance)
        {
            var tag = ('a' + rnd.Next(15)).ToString();
            var message = Guid.NewGuid().ToString();
            var sev = rnd.Next(5);
            switch (sev)
            {
                case 0: Log.Trace(tag, message); break;
                case 1: Log.Info(tag, message); break;
                case 2: Log.Warning(tag, message); break;
                case 3: Log.Error(tag, message); break;
                case 4: Log.Fatal(tag, message); break;
            }
        }
    }
}