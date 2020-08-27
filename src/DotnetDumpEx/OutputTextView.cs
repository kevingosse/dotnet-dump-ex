using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NStack;
using Terminal.Gui;
using Rune = System.Rune;

namespace DotnetDumpEx
{
    public class OutputTextView : View
    {
        TextModel model = new TextModel();
        int topRow;
        int leftColumn;
        int currentRow;
        int currentColumn;
        int selectionStartColumn, selectionStartRow;
        bool selecting;
        //bool used;

        /// <summary>
        /// Raised when the <see cref="Text"/> of the <see cref="TextView"/> changes.
        /// </summary>
        public Action TextChanged;

#if false
		/// <summary>
		///   Changed event, raised when the text has clicked.
		/// </summary>
		/// <remarks>
		///   Client code can hook up to this event, it is
		///   raised when the text in the entry changes.
		/// </remarks>
		public Action Changed;
#endif
        /// <summary>
        ///   Initializes a <see cref="TextView"/> on the specified area, with absolute position and size.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public OutputTextView(Rect frame) : base(frame)
        {
            CanFocus = true;
        }

        /// <summary>
        ///   Initializes a <see cref="TextView"/> on the specified area, 
        ///   with dimensions controlled with the X, Y, Width and Height properties.
        /// </summary>
        public OutputTextView() : base()
        {
            CanFocus = true;
        }

        void ResetPosition()
        {
            topRow = leftColumn = currentRow = currentColumn = 0;
        }

        /// <summary>
        ///   Sets or gets the text in the <see cref="TextView"/>.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public override ustring Text
        {
            get
            {
                return model.ToString();
            }

            set
            {
                ResetPosition();
                model.LoadString(value);
                TextChanged?.Invoke();
                SetNeedsDisplay();
            }
        }

        /// <summary>
        ///    Gets the current cursor row.
        /// </summary>
        public int CurrentRow => currentRow;

        /// <summary>
        /// Gets the cursor column.
        /// </summary>
        /// <value>The cursor column.</value>
        public int CurrentColumn => currentColumn;

        /// <summary>
        ///   Positions the cursor on the current row and column
        /// </summary>
        public override void PositionCursor()
        {
            if (selecting)
            {
                var minRow = Math.Min(Math.Max(Math.Min(selectionStartRow, currentRow) - topRow, 0), Frame.Height);
                var maxRow = Math.Min(Math.Max(Math.Max(selectionStartRow, currentRow) - topRow, 0), Frame.Height);

                SetNeedsDisplay(new Rect(0, minRow, Frame.Width, maxRow));
            }
            Move(CurrentColumn - leftColumn, CurrentRow - topRow);
        }

        void ClearRegion(int left, int top, int right, int bottom)
        {
            for (int row = top; row < bottom; row++)
            {
                Move(left, row);
                for (int col = left; col < right; col++)
                    AddRune(col, row, ' ');
            }
        }

        void ColorNormal()
        {
            Driver.SetAttribute(ColorScheme.Normal);
        }

        void ColorSelection()
        {
            if (HasFocus)
                Driver.SetAttribute(ColorScheme.Focus);
            else
                Driver.SetAttribute(ColorScheme.Normal);
        }

        bool isReadOnly = false;

        /// <summary>
        /// Gets or sets whether the  <see cref="TextView"/> is in read-only mode or not
        /// </summary>
        /// <value>Boolean value(Default false)</value>
        public bool ReadOnly
        {
            get => isReadOnly;
            set
            {
                isReadOnly = value;
            }
        }

        // Returns an encoded region start..end (top 32 bits are the row, low32 the column)
        void GetEncodedRegionBounds(out long start, out long end)
        {
            long selection = ((long)(uint)selectionStartRow << 32) | (uint)selectionStartColumn;
            long point = ((long)(uint)currentRow << 32) | (uint)currentColumn;
            if (selection > point)
            {
                start = point;
                end = selection;
            }
            else
            {
                start = selection;
                end = point;
            }
        }

        bool PointInSelection(int col, int row)
        {
            long start, end;
            GetEncodedRegionBounds(out start, out end);
            var q = ((long)(uint)row << 32) | (uint)col;
            return q >= start && q <= end;
        }

        //
        // Returns a ustring with the text in the selected 
        // region.
        //
        ustring GetRegion()
        {
            long start, end;
            GetEncodedRegionBounds(out start, out end);
            int startRow = (int)(start >> 32);
            var maxrow = ((int)(end >> 32));
            int startCol = (int)(start & 0xffffffff);
            var endCol = (int)(end & 0xffffffff);
            var line = model.GetLine(startRow);

            if (startRow == maxrow)
                return StringFromRunes(line.GetRange(startCol, endCol));

            ustring res = StringFromRunes(line.GetRange(startCol, line.Count - startCol));

            for (int row = startRow + 1; row < maxrow; row++)
            {
                res = res + ustring.Make((Rune)10) + StringFromRunes(model.GetLine(row));
            }
            line = model.GetLine(maxrow);
            res = res + ustring.Make((Rune)10) + StringFromRunes(line.GetRange(0, endCol));
            return res;
        }

        //
        // Clears the contents of the selected region
        //
        void ClearRegion()
        {
            long start, end;
            long currentEncoded = ((long)(uint)currentRow << 32) | (uint)currentColumn;
            GetEncodedRegionBounds(out start, out end);
            int startRow = (int)(start >> 32);
            var maxrow = ((int)(end >> 32));
            int startCol = (int)(start & 0xffffffff);
            var endCol = (int)(end & 0xffffffff);
            var line = model.GetLine(startRow);

            if (startRow == maxrow)
            {
                line.RemoveRange(startCol, endCol - startCol);
                currentColumn = startCol;
                SetNeedsDisplay(new Rect(0, startRow - topRow, Frame.Width, startRow - topRow + 1));
                return;
            }

            line.RemoveRange(startCol, line.Count - startCol);
            var line2 = model.GetLine(maxrow);
            line.AddRange(line2.Skip(endCol));
            for (int row = startRow + 1; row <= maxrow; row++)
            {
                model.RemoveLine(startRow + 1);
            }
            if (currentEncoded == end)
            {
                currentRow -= maxrow - (startRow);
            }
            currentColumn = startCol;

            SetNeedsDisplay();
        }

        ///<inheritdoc/>
        public override void Redraw(Rect bounds)
        {
            ColorNormal();

            int bottom = bounds.Bottom;
            int right = bounds.Right;
            for (int row = bounds.Top; row < bottom; row++)
            {
                int textLine = topRow + row;
                if (textLine >= model.Count)
                {
                    ColorNormal();
                    ClearRegion(bounds.Left, row, bounds.Right, row + 1);
                    continue;
                }
                var line = model.GetLine(textLine);
                int lineRuneCount = line.Count;
                if (line.Count < bounds.Left)
                {
                    ClearRegion(bounds.Left, row, bounds.Right, row + 1);
                    continue;
                }

                Move(bounds.Left, row);
                for (int col = bounds.Left; col < right; col++)
                {
                    var lineCol = leftColumn + col;
                    var rune = lineCol >= lineRuneCount ? ' ' : line[lineCol];
                    if (selecting && PointInSelection(col, row))
                        ColorSelection();
                    else
                        ColorNormal();

                    AddRune(col, row, rune);
                }
            }
            PositionCursor();
        }

        ///<inheritdoc/>
        public override bool CanFocus
        {
            get => base.CanFocus;
            set { base.CanFocus = value; }
        }

        void SetClipboard(ustring text)
        {
            Clipboard.Contents = text;
        }

        void AppendClipboard(ustring text)
        {
            Clipboard.Contents = Clipboard.Contents + text;
        }

        void Insert(Rune rune)
        {
            var line = GetCurrentLine();
            line.Insert(currentColumn, rune);
            var prow = currentRow - topRow;

            SetNeedsDisplay(new Rect(0, prow, Frame.Width, prow + 1));
        }

        ustring StringFromRunes(List<Rune> runes)
        {
            if (runes == null)
                throw new ArgumentNullException(nameof(runes));
            int size = 0;
            foreach (var rune in runes)
            {
                size += Utf8.RuneLen(rune);
            }
            var encoded = new byte[size];
            int offset = 0;
            foreach (var rune in runes)
            {
                offset += Utf8.EncodeRune(rune, encoded, offset);
            }
            return ustring.Make(encoded);
        }

        List<Rune> GetCurrentLine() => model.GetLine(currentRow);

        void InsertText(ustring text)
        {
            var lines = TextModel.StringToRunes(text);

            if (lines.Count == 0)
                return;

            var line = GetCurrentLine();

            // Optmize single line
            if (lines.Count == 1)
            {
                line.InsertRange(currentColumn, lines[0]);
                currentColumn += lines[0].Count;
                if (currentColumn - leftColumn > Frame.Width)
                    leftColumn = currentColumn - Frame.Width + 1;
                SetNeedsDisplay(new Rect(0, currentRow - topRow, Frame.Width, currentRow - topRow + 1));
                return;
            }

            // Keep a copy of the rest of the line
            var restCount = line.Count - currentColumn;
            var rest = line.GetRange(currentColumn, restCount);
            line.RemoveRange(currentColumn, restCount);

            // First line is inserted at the current location, the rest is appended
            line.InsertRange(currentColumn, lines[0]);

            for (int i = 1; i < lines.Count; i++)
                model.AddLine(currentRow + i, lines[i]);

            var last = model.GetLine(currentRow + lines.Count - 1);
            var lastp = last.Count;
            last.InsertRange(last.Count, rest);

            // Now adjjust column and row positions
            currentRow += lines.Count - 1;
            currentColumn = lastp;
            if (currentRow - topRow > Frame.Height)
            {
                topRow = currentRow - Frame.Height + 1;
                if (topRow < 0)
                    topRow = 0;
            }
            if (currentColumn < leftColumn)
                leftColumn = currentColumn;
            if (currentColumn - leftColumn >= Frame.Width)
                leftColumn = currentColumn - Frame.Width + 1;
            SetNeedsDisplay();
        }

        // The column we are tracking, or -1 if we are not tracking any column
        int columnTrack = -1;

        // Tries to snap the cursor to the tracking column
        void TrackColumn()
        {
            // Now track the column
            var line = GetCurrentLine();
            if (line.Count < columnTrack)
                currentColumn = line.Count;
            else if (columnTrack != -1)
                currentColumn = columnTrack;
            else if (currentColumn > line.Count)
                currentColumn = line.Count;
            Adjust();
        }

        void Adjust()
        {
            bool need = false;
            if (currentColumn < leftColumn)
            {
                currentColumn = leftColumn;
                need = true;
            }
            if (currentColumn - leftColumn > Frame.Width)
            {
                leftColumn = currentColumn - Frame.Width + 1;
                need = true;
            }
            if (currentRow < topRow)
            {
                topRow = currentRow;
                need = true;
            }
            if (currentRow - topRow > Frame.Height)
            {
                topRow = currentRow - Frame.Height + 1;
                need = true;
            }
            if (need)
                SetNeedsDisplay();
            else
                PositionCursor();
        }

        /// <summary>
        /// Will scroll the <see cref="TextView"/> to display the specified row at the top
        /// </summary>
        /// <param name="row">Row that should be displayed at the top, if the value is negative it will be reset to zero</param>
        public void ScrollTo(int row)
        {
            if (row < 0)
                row = 0;
            topRow = row > model.Count ? model.Count - 1 : row;
            SetNeedsDisplay();
        }

        bool lastWasKill;

        ///<inheritdoc/>
        public override bool ProcessKey(KeyEvent kb)
        {
            int restCount;
            List<Rune> rest;

            // Handle some state here - whether the last command was a kill
            // operation and the column tracking (up/down)
            switch (kb.Key)
            {
                case Key.ControlN:
                case Key.CursorDown:
                case Key.ControlP:
                case Key.CursorUp:
                    lastWasKill = false;
                    break;
                case Key.ControlK:
                    break;
                default:
                    lastWasKill = false;
                    columnTrack = -1;
                    break;
            }

            // Dispatch the command.
            switch (kb.Key)
            {
                case Key.PageDown:
                case Key.ControlV:
                    int nPageDnShift = Frame.Height - 1;
                    if (currentRow < model.Count)
                    {
                        if (columnTrack == -1)
                            columnTrack = currentColumn;
                        currentRow = (currentRow + nPageDnShift) > model.Count ? model.Count : currentRow + nPageDnShift;
                        if (topRow < currentRow - nPageDnShift)
                        {
                            topRow = currentRow >= model.Count ? currentRow - nPageDnShift : topRow + nPageDnShift;
                            SetNeedsDisplay();
                        }
                        TrackColumn();
                        PositionCursor();
                    }
                    break;

                case Key.PageUp:
                case ((int)'v' + Key.AltMask):
                    int nPageUpShift = Frame.Height - 1;
                    if (currentRow > 0)
                    {
                        if (columnTrack == -1)
                            columnTrack = currentColumn;
                        currentRow = currentRow - nPageUpShift < 0 ? 0 : currentRow - nPageUpShift;
                        if (currentRow < topRow)
                        {
                            topRow = topRow - nPageUpShift < 0 ? 0 : topRow - nPageUpShift;
                            SetNeedsDisplay();
                        }
                        TrackColumn();
                        PositionCursor();
                    }
                    break;

                case Key.ControlN:
                case Key.CursorDown:
                    MoveDown();
                    break;

                case Key.ControlP:
                case Key.CursorUp:
                    MoveUp();
                    break;

                case Key.ControlF:
                case Key.CursorRight:
                    var currentLine = GetCurrentLine();
                    if (currentColumn < currentLine.Count)
                    {
                        currentColumn++;
                        if (currentColumn >= leftColumn + Frame.Width)
                        {
                            leftColumn++;
                            SetNeedsDisplay();
                        }
                        PositionCursor();
                    }
                    else
                    {
                        if (currentRow + 1 < model.Count)
                        {
                            currentRow++;
                            currentColumn = 0;
                            leftColumn = 0;
                            if (currentRow >= topRow + Frame.Height)
                            {
                                topRow++;
                            }
                            SetNeedsDisplay();
                            PositionCursor();
                        }
                        break;
                    }
                    break;

                case Key.ControlB:
                case Key.CursorLeft:
                    if (currentColumn > 0)
                    {
                        currentColumn--;
                        if (currentColumn < leftColumn)
                        {
                            leftColumn--;
                            SetNeedsDisplay();
                        }
                        PositionCursor();
                    }
                    else
                    {
                        if (currentRow > 0)
                        {
                            currentRow--;
                            if (currentRow < topRow)
                            {
                                topRow--;
                                SetNeedsDisplay();
                            }
                            currentLine = GetCurrentLine();
                            currentColumn = currentLine.Count;
                            int prev = leftColumn;
                            leftColumn = currentColumn - Frame.Width + 1;
                            if (leftColumn < 0)
                                leftColumn = 0;
                            if (prev != leftColumn)
                                SetNeedsDisplay();
                            PositionCursor();
                        }
                    }
                    break;

                case Key.Delete:
                case Key.Backspace:
                    if (isReadOnly)
                        break;
                    if (currentColumn > 0)
                    {
                        // Delete backwards 
                        currentLine = GetCurrentLine();
                        currentLine.RemoveAt(currentColumn - 1);
                        currentColumn--;
                        if (currentColumn < leftColumn)
                        {
                            leftColumn--;
                            SetNeedsDisplay();
                        }
                        else
                            SetNeedsDisplay(new Rect(0, currentRow - topRow, 1, Frame.Width));
                    }
                    else
                    {
                        // Merges the current line with the previous one.
                        if (currentRow == 0)
                            return true;
                        var prowIdx = currentRow - 1;
                        var prevRow = model.GetLine(prowIdx);
                        var prevCount = prevRow.Count;
                        model.GetLine(prowIdx).AddRange(GetCurrentLine());
                        model.RemoveLine(currentRow);
                        currentRow--;
                        currentColumn = prevCount;
                        leftColumn = currentColumn - Frame.Width + 1;
                        if (leftColumn < 0)
                            leftColumn = 0;
                        SetNeedsDisplay();
                    }
                    break;

                // Home, C-A
                case Key.Home:
                case Key.ControlA:
                    currentColumn = 0;
                    if (currentColumn < leftColumn)
                    {
                        leftColumn = 0;
                        SetNeedsDisplay();
                    }
                    else
                        PositionCursor();
                    break;
                case Key.DeleteChar:
                case Key.ControlD: // Delete
                    if (isReadOnly)
                        break;
                    currentLine = GetCurrentLine();
                    if (currentColumn == currentLine.Count)
                    {
                        if (currentRow + 1 == model.Count)
                            break;
                        var nextLine = model.GetLine(currentRow + 1);
                        currentLine.AddRange(nextLine);
                        model.RemoveLine(currentRow + 1);
                        var sr = currentRow - topRow;
                        SetNeedsDisplay(new Rect(0, sr, Frame.Width, sr + 1));
                    }
                    else
                    {
                        currentLine.RemoveAt(currentColumn);
                        var r = currentRow - topRow;
                        SetNeedsDisplay(new Rect(currentColumn - leftColumn, r, Frame.Width, r + 1));
                    }
                    break;

                case Key.End:
                case Key.ControlE: // End
                    currentLine = GetCurrentLine();
                    currentColumn = currentLine.Count;
                    int pcol = leftColumn;
                    leftColumn = currentColumn - Frame.Width + 1;
                    if (leftColumn < 0)
                        leftColumn = 0;
                    if (pcol != leftColumn)
                        SetNeedsDisplay();
                    PositionCursor();
                    break;

                case Key.ControlK: // kill-to-end
                    if (isReadOnly)
                        break;
                    currentLine = GetCurrentLine();
                    if (currentLine.Count == 0)
                    {
                        model.RemoveLine(currentRow);
                        var val = ustring.Make((Rune)'\n');
                        if (lastWasKill)
                            AppendClipboard(val);
                        else
                            SetClipboard(val);
                    }
                    else
                    {
                        restCount = currentLine.Count - currentColumn;
                        rest = currentLine.GetRange(currentColumn, restCount);
                        var val = StringFromRunes(rest);
                        if (lastWasKill)
                            AppendClipboard(val);
                        else
                            SetClipboard(val);
                        currentLine.RemoveRange(currentColumn, restCount);
                    }
                    SetNeedsDisplay(new Rect(0, currentRow - topRow, Frame.Width, Frame.Height));
                    lastWasKill = true;
                    break;

                case Key.ControlY: // Control-y, yank
                    if (isReadOnly)
                        break;
                    InsertText(Clipboard.Contents);
                    selecting = false;
                    break;

                case Key.ControlSpace:
                    selecting = true;
                    selectionStartColumn = currentColumn;
                    selectionStartRow = currentRow;
                    break;

                case ((int)'w' + Key.AltMask):
                    SetClipboard(GetRegion());
                    selecting = false;
                    break;

                case Key.ControlW:
                    SetClipboard(GetRegion());
                    if (!isReadOnly)
                        ClearRegion();
                    selecting = false;
                    break;

                case (Key)((int)'b' + Key.AltMask):
                    var newPos = WordBackward(currentColumn, currentRow);
                    if (newPos.HasValue)
                    {
                        currentColumn = newPos.Value.col;
                        currentRow = newPos.Value.row;
                    }
                    Adjust();

                    break;

                case (Key)((int)'f' + Key.AltMask):
                    newPos = WordForward(currentColumn, currentRow);
                    if (newPos.HasValue)
                    {
                        currentColumn = newPos.Value.col;
                        currentRow = newPos.Value.row;
                    }
                    Adjust();
                    break;

                case Key.Enter:
                    if (isReadOnly)
                        break;
                    var orow = currentRow;
                    currentLine = GetCurrentLine();
                    restCount = currentLine.Count - currentColumn;
                    rest = currentLine.GetRange(currentColumn, restCount);
                    currentLine.RemoveRange(currentColumn, restCount);
                    model.AddLine(currentRow + 1, rest);
                    currentRow++;
                    bool fullNeedsDisplay = false;
                    if (currentRow >= topRow + Frame.Height)
                    {
                        topRow++;
                        fullNeedsDisplay = true;
                    }
                    currentColumn = 0;
                    if (currentColumn < leftColumn)
                    {
                        fullNeedsDisplay = true;
                        leftColumn = 0;
                    }

                    if (fullNeedsDisplay)
                        SetNeedsDisplay();
                    else
                        SetNeedsDisplay(new Rect(0, currentRow - topRow, 2, Frame.Height));
                    break;

                case Key.CtrlMask | Key.End:
                    currentRow = model.Count;
                    TrackColumn();
                    PositionCursor();
                    break;

                case Key.CtrlMask | Key.Home:
                    currentRow = 0;
                    TrackColumn();
                    PositionCursor();
                    break;

                default:
                    // Ignore control characters and other special keys
                    if (kb.Key < Key.Space || kb.Key > Key.CharMask)
                        return false;
                    //So that special keys like tab can be processed
                    if (isReadOnly)
                        return true;
                    Insert((uint)kb.Key);
                    currentColumn++;
                    if (currentColumn >= leftColumn + Frame.Width)
                    {
                        leftColumn++;
                        SetNeedsDisplay();
                    }
                    PositionCursor();
                    return true;
            }
            return true;
        }

        private void MoveUp()
        {
            if (currentRow > 0)
            {
                if (columnTrack == -1)
                    columnTrack = currentColumn;
                currentRow--;
                if (currentRow < topRow)
                {
                    topRow--;
                    SetNeedsDisplay();
                }
                TrackColumn();
                PositionCursor();
            }
        }

        private void MoveDown()
        {
            if (currentRow + 1 < model.Count)
            {
                if (columnTrack == -1)
                    columnTrack = currentColumn;
                currentRow++;
                if (currentRow >= topRow + Frame.Height)
                {
                    topRow++;
                    SetNeedsDisplay();
                }
                TrackColumn();
                PositionCursor();
            }
        }

        IEnumerable<(int col, int row, Rune rune)> ForwardIterator(int col, int row)
        {
            if (col < 0 || row < 0)
                yield break;
            if (row >= model.Count)
                yield break;
            var line = GetCurrentLine();
            if (col >= line.Count)
                yield break;

            while (row < model.Count)
            {
                for (int c = col; c < line.Count; c++)
                {
                    yield return (c, row, line[c]);
                }
                col = 0;
                row++;
                line = GetCurrentLine();
            }
        }

        Rune RuneAt(int col, int row) => model.GetLine(row)[col];

        bool MoveNext(ref int col, ref int row, out Rune rune)
        {
            var line = model.GetLine(row);
            if (col + 1 < line.Count)
            {
                col++;
                rune = line[col];
                return true;
            }
            while (row + 1 < model.Count)
            {
                col = 0;
                row++;
                line = model.GetLine(row);
                if (line.Count > 0)
                {
                    rune = line[0];
                    return true;
                }
            }
            rune = 0;
            return false;
        }

        bool MovePrev(ref int col, ref int row, out Rune rune)
        {
            var line = model.GetLine(row);

            if (col > 0)
            {
                col--;
                rune = line[col];
                return true;
            }
            if (row == 0)
            {
                rune = 0;
                return false;
            }
            while (row > 0)
            {
                row--;
                line = model.GetLine(row);
                col = line.Count - 1;
                if (col >= 0)
                {
                    rune = line[col];
                    return true;
                }
            }
            rune = 0;
            return false;
        }

        (int col, int row)? WordForward(int fromCol, int fromRow)
        {
            var col = fromCol;
            var row = fromRow;
            var line = GetCurrentLine();
            var rune = RuneAt(col, row);

            var srow = row;
            if (Rune.IsPunctuation(rune) || Rune.IsWhiteSpace(rune))
            {
                while (MoveNext(ref col, ref row, out rune))
                {
                    if (Rune.IsLetterOrDigit(rune))
                        break;
                }
                while (MoveNext(ref col, ref row, out rune))
                {
                    if (!Rune.IsLetterOrDigit(rune))
                        break;
                }
            }
            else
            {
                while (MoveNext(ref col, ref row, out rune))
                {
                    if (!Rune.IsLetterOrDigit(rune))
                        break;
                }
            }
            if (fromCol != col || fromRow != row)
                return (col, row);
            return null;
        }

        (int col, int row)? WordBackward(int fromCol, int fromRow)
        {
            if (fromRow == 0 && fromCol == 0)
                return null;

            var col = fromCol;
            var row = fromRow;
            var line = GetCurrentLine();
            var rune = RuneAt(col, row);

            if (Rune.IsPunctuation(rune) || Rune.IsSymbol(rune) || Rune.IsWhiteSpace(rune))
            {
                while (MovePrev(ref col, ref row, out rune))
                {
                    if (Rune.IsLetterOrDigit(rune))
                        break;
                }
                while (MovePrev(ref col, ref row, out rune))
                {
                    if (!Rune.IsLetterOrDigit(rune))
                        break;
                }
            }
            else
            {
                while (MovePrev(ref col, ref row, out rune))
                {
                    if (!Rune.IsLetterOrDigit(rune))
                        break;
                }
            }
            if (fromCol != col || fromRow != row)
                return (col, row);
            return null;
        }

        ///<inheritdoc/>
        public override bool MouseEvent(MouseEvent ev)
        {
            if (!ev.Flags.HasFlag(MouseFlags.Button1Clicked) &&
                !ev.Flags.HasFlag(MouseFlags.WheeledDown) && !ev.Flags.HasFlag(MouseFlags.WheeledUp))
            {
                return false;
            }

            if (!CanFocus)
            {
                return true;
            }

            if (!HasFocus)
            {
                SetFocus();
            }

            if (ev.Flags == MouseFlags.Button1Clicked)
            {
                if (model.Count > 0)
                {
                    var maxCursorPositionableLine = (model.Count - 1) - topRow;
                    if (ev.Y > maxCursorPositionableLine)
                    {
                        currentRow = maxCursorPositionableLine;
                    }
                    else
                    {
                        currentRow = ev.Y + topRow;
                    }
                    var r = GetCurrentLine();
                    if (ev.X - leftColumn >= r.Count)
                        currentColumn = r.Count - leftColumn;
                    else
                        currentColumn = ev.X - leftColumn;
                }
                PositionCursor();
            }
            else if (ev.Flags == MouseFlags.WheeledDown)
            {
                lastWasKill = false;
                MoveDown();
            }
            else if (ev.Flags == MouseFlags.WheeledUp)
            {
                lastWasKill = false;
                MoveUp();
            }

            return true;
        }

        public void Append(string line)
        {
            model.AddLine(line);
            ScrollToEnd();
            SetNeedsDisplay();
        }

        public void ScrollToEnd()
        {
            var height = Frame.Height;

            if (model.Count > height)
            {
                ScrollTo(model.Count - height);
            }
        }

        class TextModel
        {
            List<List<Rune>> lines = new List<List<Rune>>();

            // Turns the ustring into runes, this does not split the 
            // contents on a newline if it is present.
            internal static List<Rune> ToRunes(ustring str)
            {
                List<Rune> runes = new List<Rune>();
                foreach (var x in str.ToRunes())
                {
                    runes.Add(x);
                }
                return runes;
            }

            // Splits a string into a List that contains a List<Rune> for each line
            public static List<List<Rune>> StringToRunes(ustring content)
            {
                var lines = new List<List<Rune>>();
                int start = 0, i = 0;
                // BUGBUG: I think this is buggy w.r.t Unicode. content.Length is bytes, and content[i] is bytes
                // and content[i] == 10 may be the middle of a Rune.
                for (; i < content.Length; i++)
                {
                    if (content[i] == 10)
                    {
                        if (i - start > 0)
                            lines.Add(ToRunes(content[start, i]));
                        else
                            lines.Add(ToRunes(ustring.Empty));
                        start = i + 1;
                    }
                }
                if (i - start >= 0)
                    lines.Add(ToRunes(content[start, null]));
                return lines;
            }

            public void LoadString(ustring content)
            {
                lines = StringToRunes(content);
            }

            public override string ToString()
            {
                var sb = new StringBuilder();
                for (int i = 0; i < lines.Count; i++)
                {
                    sb.Append(ustring.Make(lines[i]));
                    if ((i + 1) < lines.Count)
                    {
                        sb.AppendLine();
                    }
                }
                return sb.ToString();
            }

            /// <summary>
            /// The number of text lines in the model
            /// </summary>
            public int Count => lines.Count;

            /// <summary>
            /// Returns the specified line as a List of Rune
            /// </summary>
            /// <returns>The line.</returns>
            /// <param name="line">Line number to retrieve.</param>
            public List<Rune> GetLine(int line) => line < Count ? lines[line] : lines[Count - 1];

            public void AddLine(string line)
            {
                lines.Add(ToRunes(line));
            }

            /// <summary>
            /// Adds a line to the model at the specified position.
            /// </summary>
            /// <param name="pos">Line number where the line will be inserted.</param>
            /// <param name="runes">The line of text, as a List of Rune.</param>
            public void AddLine(int pos, List<Rune> runes)
            {
                lines.Insert(pos, runes);
            }

            /// <summary>
            /// Removes the line at the specified position
            /// </summary>
            /// <param name="pos">Position.</param>
            public void RemoveLine(int pos)
            {
                lines.RemoveAt(pos);
            }
        }
    }
}