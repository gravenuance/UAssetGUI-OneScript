UAGUtils.InvokeUI(() =>
{
    try
    {
        var form = Interface.GetBaseForm();
        var formType = form.GetType();
        var flags =
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic;

        var treeField = formType.GetField("treeView1", flags);
        var dgvField = formType.GetField("dataGridView1", flags);

        if (treeField == null || dgvField == null)
        {
            System.Windows.Forms.MessageBox.Show(
                "Could not find treeView1 or dataGridView1.",
                "Script Error");
            return;
        }

        var tree = treeField.GetValue(form) as System.Windows.Forms.TreeView;
        var dataGridView = dgvField.GetValue(form) as System.Windows.Forms.DataGridView;

        if (tree == null || dataGridView == null)
        {
            System.Windows.Forms.MessageBox.Show(
                "treeView1 or dataGridView1 is null.",
                "Script Error");
            return;
        }

        string configDirectory = System.Windows.Forms.Application.LocalUserAppDataPath;
        string configFilePath = System.IO.Path.Combine(configDirectory, "UAssetGUI_BatchRuleEditor_LastConfig.txt");

        System.Windows.Forms.TreeNode FindChildNode(System.Windows.Forms.TreeNode parent, string startsWith)
        {
            foreach (System.Windows.Forms.TreeNode child in parent.Nodes)
            {
                if (child.Text.StartsWith(startsWith))
                    return child;
            }

            return null;
        }

        void SelectNode(System.Windows.Forms.TreeNode node)
        {
            tree.SelectedNode = node;
            node.EnsureVisible();
            tree.Focus();
            System.Windows.Forms.Application.DoEvents();
            System.Threading.Thread.Sleep(30);
            System.Windows.Forms.Application.DoEvents();
        }

        bool TryParseBool(object value, out bool result)
        {
            result = false;
            if (value == null) return false;

            string text = value.ToString().Trim();

            if (text.Equals("true", System.StringComparison.OrdinalIgnoreCase) || text == "1")
            {
                result = true;
                return true;
            }

            if (text.Equals("false", System.StringComparison.OrdinalIgnoreCase) || text == "0")
            {
                result = false;
                return true;
            }

            return bool.TryParse(text, out result);
        }

        bool TryParseInt(object value, out int result)
        {
            result = 0;
            if (value == null) return false;

            string text = value.ToString().Trim();
            return int.TryParse(
                text,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out result)
                || int.TryParse(text, out result);
        }

        bool TryParseFloat(object value, out float result)
        {
            result = 0f;
            if (value == null) return false;

            string text = value.ToString().Trim();
            return float.TryParse(
                text,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out result)
                || float.TryParse(text, out result);
        }

        string FormatFloat(float value)
        {
            return System.Math.Round(value, 6).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        bool ContainsToken(string source, string token)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(token))
                return false;

            return source.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        bool EntryMatchesConditions(
            string entryName,
            System.Collections.Generic.List<string> conditions,
            bool useAndLogic)
        {
            if (string.IsNullOrWhiteSpace(entryName))
                return false;

            var filteredConditions = new System.Collections.Generic.List<string>();
            foreach (string condition in conditions)
            {
                if (!string.IsNullOrWhiteSpace(condition))
                    filteredConditions.Add(condition.Trim());
            }

            if (filteredConditions.Count == 0)
                return true;

            if (useAndLogic)
            {
                foreach (string condition in filteredConditions)
                {
                    if (!ContainsToken(entryName, condition))
                        return false;
                }

                return true;
            }

            foreach (string condition in filteredConditions)
            {
                if (ContainsToken(entryName, condition))
                    return true;
            }

            return false;
        }

        bool IsNullLikeByType(string type, object value)
        {
            if (value == null)
                return true;

            string normalizedType = (type ?? string.Empty).Trim().ToLowerInvariant();

            switch (normalizedType)
            {
                case "int":
                {
                    int parsed;
                    return TryParseInt(value, out parsed) && parsed == 0;
                }
                case "float":
                {
                    float parsed;
                    return TryParseFloat(value, out parsed) && System.Math.Abs(parsed) < 0.000001f;
                }
                case "bool":
                {
                    bool parsed;
                    return TryParseBool(value, out parsed) && parsed == false;
                }
                case "string":
                {
                    return string.IsNullOrWhiteSpace(value.ToString());
                }
                default:
                {
                    string text = value.ToString().Trim();
                    return text.Length == 0 || text == "0" || text.Equals("false", System.StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        bool EvaluateSkipNumeric(string skipOperation, float currentValue, float skipValue)
        {
            switch ((skipOperation ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "eq": return System.Math.Abs(currentValue - skipValue) < 0.000001f;
                case "lt": return currentValue < skipValue;
                case "gt": return currentValue > skipValue;
                case "lte": return currentValue <= skipValue;
                case "gte": return currentValue >= skipValue;
                default: return false;
            }
        }

        int ApplyIntOperation(int currentValue, string operation, int targetValue)
        {
            switch ((operation ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "set": return targetValue;
                case "add": return currentValue + targetValue;
                case "sub": return currentValue - targetValue;
                case "mul": return currentValue * targetValue;
                case "div": return targetValue == 0 ? currentValue : currentValue / targetValue;
                default: return currentValue;
            }
        }

        float ApplyFloatOperation(float currentValue, string operation, float targetValue)
        {
            switch ((operation ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "set": return targetValue;
                case "add": return currentValue + targetValue;
                case "sub": return currentValue - targetValue;
                case "mul": return currentValue * targetValue;
                case "div": return System.Math.Abs(targetValue) < 0.000001f ? currentValue : currentValue / targetValue;
                default: return currentValue;
            }
        }

        string EscapeConfig(string value)
        {
            if (value == null) return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("|", "\\p");
        }

        string UnescapeConfig(string value)
        {
            if (value == null) return string.Empty;

            var sb = new System.Text.StringBuilder();
            bool escaping = false;

            foreach (char c in value)
            {
                if (escaping)
                {
                    switch (c)
                    {
                        case 'r': sb.Append('\r'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'p': sb.Append('|'); break;
                        case '\\': sb.Append('\\'); break;
                        default:
                            sb.Append(c);
                            break;
                    }

                    escaping = false;
                }
                else
                {
                    if (c == '\\')
                        escaping = true;
                    else
                        sb.Append(c);
                }
            }

            if (escaping)
                sb.Append('\\');

            return sb.ToString();
        }

        string[] SplitEscapedPipe(string line)
        {
            var parts = new System.Collections.Generic.List<string>();
            var sb = new System.Text.StringBuilder();
            bool escaping = false;

            foreach (char c in line)
            {
                if (escaping)
                {
                    sb.Append('\\');
                    sb.Append(c);
                    escaping = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaping = true;
                    continue;
                }

                if (c == '|')
                {
                    parts.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }

                sb.Append(c);
            }

            if (escaping)
                sb.Append('\\');

            parts.Add(sb.ToString());
            return parts.ToArray();
        }

        int nameColumnIndex = -1;
        int valueColumnIndex = -1;
        int isZeroColumnIndex = -1;

        for (int columnIndex = 0; columnIndex < dataGridView.Columns.Count; columnIndex++)
        {
            string headerText = dataGridView.Columns[columnIndex].HeaderText;
            if (headerText == "Name") nameColumnIndex = columnIndex;
            if (headerText == "Value") valueColumnIndex = columnIndex;
            if (headerText == "Is Zero") isZeroColumnIndex = columnIndex;
        }

        if (nameColumnIndex < 0 || valueColumnIndex < 0)
        {
            System.Windows.Forms.MessageBox.Show(
                "Could not find Name/Value columns in dataGridView1.",
                "Script Error");
            return;
        }

        var configForm = new System.Windows.Forms.Form();
        configForm.Text = "Batch Rule Editor";
        configForm.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        configForm.Width = 1220;
        configForm.Height = 720;
        configForm.MinimizeBox = false;
        configForm.MaximizeBox = true;
        configForm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
        configForm.ShowInTaskbar = false;

        var filtersLabel = new System.Windows.Forms.Label();
        filtersLabel.Text = "Entry name conditions (one per line):";
        filtersLabel.Left = 12;
        filtersLabel.Top = 12;
        filtersLabel.Width = 260;

        var filtersTextBox = new System.Windows.Forms.TextBox();
        filtersTextBox.Left = 12;
        filtersTextBox.Top = 34;
        filtersTextBox.Width = 260;
        filtersTextBox.Height = 90;
        filtersTextBox.Multiline = true;
        filtersTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        filtersTextBox.Text = "Example" + System.Environment.NewLine + "_Demo";

        var logicLabel = new System.Windows.Forms.Label();
        logicLabel.Text = "Condition logic:";
        logicLabel.Left = 290;
        logicLabel.Top = 12;
        logicLabel.Width = 120;

        var logicComboBox = new System.Windows.Forms.ComboBox();
        logicComboBox.Left = 290;
        logicComboBox.Top = 34;
        logicComboBox.Width = 120;
        logicComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        logicComboBox.Items.Add("AND");
        logicComboBox.Items.Add("OR");
        logicComboBox.SelectedIndex = 0;

        var helpLabel = new System.Windows.Forms.Label();
        helpLabel.Left = 430;
        helpLabel.Top = 12;
        helpLabel.Width = 760;
        helpLabel.Height = 112;
        helpLabel.Text =
            "This dialog auto-saves the last used configuration.\r\n" +
            "Default rows are generic examples:\r\n" +
            "- float example: multiply a value\r\n" +
            "- int example: add a value\r\n" +
            "- bool example: set a flag to true\r\n" +
            "Edit these examples to fit your actual DataTable property names before running.";

        var rulesGrid = new System.Windows.Forms.DataGridView();
        rulesGrid.Left = 12;
        rulesGrid.Top = 140;
        rulesGrid.Width = 1180;
        rulesGrid.Height = 480;
        rulesGrid.Anchor =
            System.Windows.Forms.AnchorStyles.Top |
            System.Windows.Forms.AnchorStyles.Bottom |
            System.Windows.Forms.AnchorStyles.Left |
            System.Windows.Forms.AnchorStyles.Right;
        rulesGrid.AllowUserToAddRows = true;
        rulesGrid.AllowUserToDeleteRows = true;
        rulesGrid.AllowUserToResizeRows = false;
        rulesGrid.RowHeadersVisible = false;
        rulesGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
        rulesGrid.MultiSelect = false;
        rulesGrid.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
        rulesGrid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.None;

        var enabledColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
        enabledColumn.Name = "Enabled";
        enabledColumn.HeaderText = "Enabled";
        enabledColumn.Width = 60;

        var propNameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
        propNameColumn.Name = "PropName";
        propNameColumn.HeaderText = "PropName";
        propNameColumn.Width = 180;

        var typeColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
        typeColumn.Name = "Type";
        typeColumn.HeaderText = "Type";
        typeColumn.Width = 80;
        typeColumn.DisplayStyle = System.Windows.Forms.DataGridViewComboBoxDisplayStyle.DropDownButton;
        typeColumn.Items.AddRange(new object[] { "int", "float", "string", "bool" });

        var targetOperationColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
        targetOperationColumn.Name = "TargetOperation";
        targetOperationColumn.HeaderText = "TargetOperation";
        targetOperationColumn.Width = 110;
        targetOperationColumn.DisplayStyle = System.Windows.Forms.DataGridViewComboBoxDisplayStyle.DropDownButton;
        targetOperationColumn.Items.AddRange(new object[] { "set", "add", "sub", "mul", "div" });

        var targetValueColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
        targetValueColumn.Name = "TargetValue";
        targetValueColumn.HeaderText = "TargetValue";
        targetValueColumn.Width = 120;

        var useSkipColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
        useSkipColumn.Name = "UseSkip";
        useSkipColumn.HeaderText = "UseSkip";
        useSkipColumn.Width = 70;

        var skipOperationColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
        skipOperationColumn.Name = "SkipOperation";
        skipOperationColumn.HeaderText = "SkipOperation";
        skipOperationColumn.Width = 110;
        skipOperationColumn.DisplayStyle = System.Windows.Forms.DataGridViewComboBoxDisplayStyle.DropDownButton;
        skipOperationColumn.Items.AddRange(new object[] { "eq", "lt", "gt", "lte", "gte" });

        var skipValueColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
        skipValueColumn.Name = "SkipValue";
        skipValueColumn.HeaderText = "SkipValue";
        skipValueColumn.Width = 120;

        var setIsZeroColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
        setIsZeroColumn.Name = "SetIsZeroWhenNullLike";
        setIsZeroColumn.HeaderText = "Set Is Zero When Null-Like";
        setIsZeroColumn.Width = 150;

        rulesGrid.Columns.Add(enabledColumn);
        rulesGrid.Columns.Add(propNameColumn);
        rulesGrid.Columns.Add(typeColumn);
        rulesGrid.Columns.Add(targetOperationColumn);
        rulesGrid.Columns.Add(targetValueColumn);
        rulesGrid.Columns.Add(useSkipColumn);
        rulesGrid.Columns.Add(skipOperationColumn);
        rulesGrid.Columns.Add(skipValueColumn);
        rulesGrid.Columns.Add(setIsZeroColumn);

        rulesGrid.CurrentCellDirtyStateChanged += (sender, args) =>
        {
            if (rulesGrid.IsCurrentCellDirty)
                rulesGrid.CommitEdit(System.Windows.Forms.DataGridViewDataErrorContexts.Commit);
        };

        rulesGrid.DataError += (sender, args) =>
        {
            args.ThrowException = false;
        };

        void AddRuleRow(
            bool enabled,
            string propName,
            string type,
            string targetOperation,
            string targetValue,
            bool useSkip,
            string skipOperation,
            string skipValue,
            bool setIsZeroWhenNullLike)
        {
            rulesGrid.Rows.Add(
                enabled,
                propName,
                type,
                targetOperation,
                targetValue,
                useSkip,
                skipOperation,
                skipValue,
                setIsZeroWhenNullLike);
        }

        void LoadGenericExampleRows()
        {
            rulesGrid.Rows.Clear();

            AddRuleRow(true, "ExampleFloatProp", "float", "mul", "0.9", true, "eq", "0", true);
            AddRuleRow(true, "ExampleIntProp", "int", "add", "5", true, "lt", "0", true);
            AddRuleRow(true, "ExampleBoolProp", "bool", "set", "true", true, "eq", "true", true);
        }

        bool LoadSavedConfig()
        {
            try
            {
                if (!System.IO.File.Exists(configFilePath))
                    return false;

                string[] lines = System.IO.File.ReadAllLines(configFilePath);
                if (lines == null || lines.Length == 0)
                    return false;

                filtersTextBox.Clear();
                rulesGrid.Rows.Clear();

                foreach (string rawLine in lines)
                {
                    if (string.IsNullOrWhiteSpace(rawLine))
                        continue;

                    if (rawLine.StartsWith("LOGIC|"))
                    {
                        var parts = SplitEscapedPipe(rawLine);
                        if (parts.Length >= 2)
                        {
                            string logic = UnescapeConfig(parts[1]).Trim().ToUpperInvariant();
                            logicComboBox.SelectedItem = logic == "OR" ? "OR" : "AND";
                        }
                    }
                    else if (rawLine.StartsWith("FILTER|"))
                    {
                        var parts = SplitEscapedPipe(rawLine);
                        if (parts.Length >= 2)
                        {
                            string filterValue = UnescapeConfig(parts[1]);
                            if (filtersTextBox.TextLength > 0)
                                filtersTextBox.AppendText(System.Environment.NewLine);

                            filtersTextBox.AppendText(filterValue);
                        }
                    }
                    else if (rawLine.StartsWith("RULE|"))
                    {
                        var parts = SplitEscapedPipe(rawLine);
                        if (parts.Length >= 10)
                        {
                            bool enabled = false;
                            bool useSkip = false;
                            bool setIsZeroWhenNullLike = true;

                            bool parsedBool;
                            if (TryParseBool(UnescapeConfig(parts[1]), out parsedBool)) enabled = parsedBool;
                            if (TryParseBool(UnescapeConfig(parts[6]), out parsedBool)) useSkip = parsedBool;
                            if (TryParseBool(UnescapeConfig(parts[9]), out parsedBool)) setIsZeroWhenNullLike = parsedBool;

                            AddRuleRow(
                                enabled,
                                UnescapeConfig(parts[2]),
                                UnescapeConfig(parts[3]),
                                UnescapeConfig(parts[4]),
                                UnescapeConfig(parts[5]),
                                useSkip,
                                UnescapeConfig(parts[7]),
                                UnescapeConfig(parts[8]),
                                setIsZeroWhenNullLike);
                        }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        void SaveCurrentConfig()
        {
            var lines = new System.Collections.Generic.List<string>();

            string logicValue = logicComboBox.SelectedItem == null ? "AND" : logicComboBox.SelectedItem.ToString();
            lines.Add("LOGIC|" + EscapeConfig(logicValue));

            foreach (string filterLine in filtersTextBox.Lines)
            {
                if (string.IsNullOrWhiteSpace(filterLine))
                    continue;

                lines.Add("FILTER|" + EscapeConfig(filterLine.Trim()));
            }

            foreach (System.Windows.Forms.DataGridViewRow row in rulesGrid.Rows)
            {
                if (row.IsNewRow) continue;

                string enabled = row.Cells["Enabled"].Value == null ? "False" : row.Cells["Enabled"].Value.ToString();
                string propName = row.Cells["PropName"].Value == null ? string.Empty : row.Cells["PropName"].Value.ToString();
                string type = row.Cells["Type"].Value == null ? string.Empty : row.Cells["Type"].Value.ToString();
                string targetOperation = row.Cells["TargetOperation"].Value == null ? string.Empty : row.Cells["TargetOperation"].Value.ToString();
                string targetValue = row.Cells["TargetValue"].Value == null ? string.Empty : row.Cells["TargetValue"].Value.ToString();
                string useSkip = row.Cells["UseSkip"].Value == null ? "False" : row.Cells["UseSkip"].Value.ToString();
                string skipOperation = row.Cells["SkipOperation"].Value == null ? string.Empty : row.Cells["SkipOperation"].Value.ToString();
                string skipValue = row.Cells["SkipValue"].Value == null ? string.Empty : row.Cells["SkipValue"].Value.ToString();
                string setIsZeroWhenNullLike = row.Cells["SetIsZeroWhenNullLike"].Value == null ? "False" : row.Cells["SetIsZeroWhenNullLike"].Value.ToString();

                lines.Add(
                    "RULE|" +
                    EscapeConfig(enabled) + "|" +
                    EscapeConfig(propName) + "|" +
                    EscapeConfig(type) + "|" +
                    EscapeConfig(targetOperation) + "|" +
                    EscapeConfig(targetValue) + "|" +
                    EscapeConfig(useSkip) + "|" +
                    EscapeConfig(skipOperation) + "|" +
                    EscapeConfig(skipValue) + "|" +
                    EscapeConfig(setIsZeroWhenNullLike));
            }

            if (!System.IO.Directory.Exists(configDirectory))
                System.IO.Directory.CreateDirectory(configDirectory);

            System.IO.File.WriteAllText(
                configFilePath,
                string.Join(System.Environment.NewLine, lines.ToArray()),
                System.Text.Encoding.UTF8);
        }

        bool loadedSavedConfig = LoadSavedConfig();
        if (!loadedSavedConfig)
            LoadGenericExampleRows();

        var addRowButton = new System.Windows.Forms.Button();
        addRowButton.Text = "Add Rule";
        addRowButton.Left = 12;
        addRowButton.Top = 632;
        addRowButton.Width = 100;
        addRowButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Bottom;
        addRowButton.Click += (sender, args) =>
        {
            AddRuleRow(true, "", "bool", "set", "", false, "eq", "", true);
        };

        var removeRowButton = new System.Windows.Forms.Button();
        removeRowButton.Text = "Remove Selected";
        removeRowButton.Left = 120;
        removeRowButton.Top = 632;
        removeRowButton.Width = 130;
        removeRowButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Bottom;
        removeRowButton.Click += (sender, args) =>
        {
            if (rulesGrid.SelectedRows.Count > 0)
            {
                foreach (System.Windows.Forms.DataGridViewRow selectedRow in rulesGrid.SelectedRows)
                {
                    if (!selectedRow.IsNewRow)
                        rulesGrid.Rows.Remove(selectedRow);
                }
            }
        };

        var resetExamplesButton = new System.Windows.Forms.Button();
        resetExamplesButton.Text = "Load Example Rules";
        resetExamplesButton.Left = 260;
        resetExamplesButton.Top = 632;
        resetExamplesButton.Width = 140;
        resetExamplesButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Bottom;
        resetExamplesButton.Click += (sender, args) =>
        {
            var result = System.Windows.Forms.MessageBox.Show(
                "Replace the current rule grid with generic example rules?",
                "Confirm Reset",
                System.Windows.Forms.MessageBoxButtons.YesNo,
                System.Windows.Forms.MessageBoxIcon.Question);

            if (result == System.Windows.Forms.DialogResult.Yes)
                LoadGenericExampleRows();
        };

        var okButton = new System.Windows.Forms.Button();
        okButton.Text = "OK";
        okButton.Width = 100;
        okButton.Height = 30;
        okButton.Left = 980;
        okButton.Top = 632;
        okButton.Anchor = System.Windows.Forms.AnchorStyles.Right | System.Windows.Forms.AnchorStyles.Bottom;
        okButton.DialogResult = System.Windows.Forms.DialogResult.None;

        var cancelButton = new System.Windows.Forms.Button();
        cancelButton.Text = "Cancel";
        cancelButton.Width = 100;
        cancelButton.Height = 30;
        cancelButton.Left = 1092;
        cancelButton.Top = 632;
        cancelButton.Anchor = System.Windows.Forms.AnchorStyles.Right | System.Windows.Forms.AnchorStyles.Bottom;
        cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;

        configForm.Controls.Add(filtersLabel);
        configForm.Controls.Add(filtersTextBox);
        configForm.Controls.Add(logicLabel);
        configForm.Controls.Add(logicComboBox);
        configForm.Controls.Add(helpLabel);
        configForm.Controls.Add(rulesGrid);
        configForm.Controls.Add(addRowButton);
        configForm.Controls.Add(removeRowButton);
        configForm.Controls.Add(resetExamplesButton);
        configForm.Controls.Add(okButton);
        configForm.Controls.Add(cancelButton);

        configForm.AcceptButton = okButton;
        configForm.CancelButton = cancelButton;

        var entryNameConditions = new System.Collections.Generic.List<string>();
        bool useAndLogicForEntryName = true;

        var parsedRules = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();

        okButton.Click += (sender, args) =>
        {
            try
            {
                rulesGrid.EndEdit();
                dataGridView.EndEdit();

                var tempConditions = new System.Collections.Generic.List<string>();
                foreach (string line in filtersTextBox.Lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        tempConditions.Add(line.Trim());
                }

                bool tempUseAndLogic = logicComboBox.SelectedItem == null ||
                                       logicComboBox.SelectedItem.ToString().Equals("AND", System.StringComparison.OrdinalIgnoreCase);

                var tempRules = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();

                foreach (System.Windows.Forms.DataGridViewRow row in rulesGrid.Rows)
                {
                    if (row.IsNewRow) continue;

                    bool enabled = false;
                    object enabledObject = row.Cells["Enabled"].Value;
                    if (enabledObject != null)
                    {
                        bool parsedEnabled;
                        if (TryParseBool(enabledObject, out parsedEnabled))
                            enabled = parsedEnabled;
                    }

                    if (!enabled)
                        continue;

                    string propName = row.Cells["PropName"].Value == null
                        ? string.Empty
                        : row.Cells["PropName"].Value.ToString().Trim();

                    string type = row.Cells["Type"].Value == null
                        ? string.Empty
                        : row.Cells["Type"].Value.ToString().Trim().ToLowerInvariant();

                    string targetOperation = row.Cells["TargetOperation"].Value == null
                        ? "set"
                        : row.Cells["TargetOperation"].Value.ToString().Trim().ToLowerInvariant();

                    string targetValueText = row.Cells["TargetValue"].Value == null
                        ? string.Empty
                        : row.Cells["TargetValue"].Value.ToString().Trim();

                    bool useSkip = false;
                    object useSkipObject = row.Cells["UseSkip"].Value;
                    if (useSkipObject != null)
                    {
                        bool parsedUseSkip;
                        if (TryParseBool(useSkipObject, out parsedUseSkip))
                            useSkip = parsedUseSkip;
                    }

                    string skipOperation = row.Cells["SkipOperation"].Value == null
                        ? "eq"
                        : row.Cells["SkipOperation"].Value.ToString().Trim().ToLowerInvariant();

                    string skipValueText = row.Cells["SkipValue"].Value == null
                        ? string.Empty
                        : row.Cells["SkipValue"].Value.ToString().Trim();

                    bool setIsZeroWhenNullLike = true;
                    object setIsZeroObject = row.Cells["SetIsZeroWhenNullLike"].Value;
                    if (setIsZeroObject != null)
                    {
                        bool parsedSetIsZero;
                        if (TryParseBool(setIsZeroObject, out parsedSetIsZero))
                            setIsZeroWhenNullLike = parsedSetIsZero;
                    }

                    if (propName.Length == 0)
                    {
                        System.Windows.Forms.MessageBox.Show(
                            "Enabled rule has an empty PropName.",
                            "Validation Error");
                        return;
                    }

                    if (type != "int" && type != "float" && type != "string" && type != "bool")
                    {
                        System.Windows.Forms.MessageBox.Show(
                            "Rule for '" + propName + "' has an invalid Type.",
                            "Validation Error");
                        return;
                    }

                    if (targetValueText.Length == 0)
                    {
                        System.Windows.Forms.MessageBox.Show(
                            "Rule for '" + propName + "' is missing TargetValue.",
                            "Validation Error");
                        return;
                    }

                    var rule = new System.Collections.Generic.Dictionary<string, object>(System.StringComparer.OrdinalIgnoreCase);
                    rule["PropName"] = propName;
                    rule["Type"] = type;
                    rule["TargetOperation"] = targetOperation;
                    rule["SetIsZeroWhenNullLike"] = setIsZeroWhenNullLike;

                    switch (type)
                    {
                        case "bool":
                        {
                            bool targetBool;
                            if (!TryParseBool(targetValueText, out targetBool))
                            {
                                System.Windows.Forms.MessageBox.Show(
                                    "Rule for '" + propName + "' has an invalid bool TargetValue.",
                                    "Validation Error");
                                return;
                            }

                            rule["TargetValue"] = targetBool;

                            if (useSkip)
                            {
                                if (skipValueText.Length == 0)
                                {
                                    System.Windows.Forms.MessageBox.Show(
                                        "Rule for '" + propName + "' has UseSkip enabled but no SkipValue.",
                                        "Validation Error");
                                    return;
                                }

                                bool skipBool;
                                if (!TryParseBool(skipValueText, out skipBool))
                                {
                                    System.Windows.Forms.MessageBox.Show(
                                        "Rule for '" + propName + "' has an invalid bool SkipValue.",
                                        "Validation Error");
                                    return;
                                }

                                rule["UseSkip"] = true;
                                rule["SkipOperation"] = "eq";
                                rule["SkipValue"] = skipBool;
                            }
                            else
                            {
                                rule["UseSkip"] = false;
                            }

                            break;
                        }

                        case "string":
                        {
                            rule["TargetValue"] = targetValueText;

                            if (useSkip)
                            {
                                if (skipValueText.Length == 0)
                                {
                                    System.Windows.Forms.MessageBox.Show(
                                        "Rule for '" + propName + "' has UseSkip enabled but no SkipValue.",
                                        "Validation Error");
                                    return;
                                }

                                rule["UseSkip"] = true;
                                rule["SkipOperation"] = "eq";
                                rule["SkipValue"] = skipValueText;
                            }
                            else
                            {
                                rule["UseSkip"] = false;
                            }

                            break;
                        }

                        case "int":
                        {
                            int targetInt;
                            if (!int.TryParse(
                                targetValueText,
                                System.Globalization.NumberStyles.Integer,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out targetInt))
                            {
                                System.Windows.Forms.MessageBox.Show(
                                    "Rule for '" + propName + "' has an invalid int TargetValue.",
                                    "Validation Error");
                                return;
                            }

                            rule["TargetValue"] = targetInt;

                            if (useSkip)
                            {
                                if (skipValueText.Length == 0)
                                {
                                    System.Windows.Forms.MessageBox.Show(
                                        "Rule for '" + propName + "' has UseSkip enabled but no SkipValue.",
                                        "Validation Error");
                                    return;
                                }

                                int skipInt;
                                if (!int.TryParse(
                                    skipValueText,
                                    System.Globalization.NumberStyles.Integer,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out skipInt))
                                {
                                    System.Windows.Forms.MessageBox.Show(
                                        "Rule for '" + propName + "' has an invalid int SkipValue.",
                                        "Validation Error");
                                    return;
                                }

                                rule["UseSkip"] = true;
                                rule["SkipOperation"] = skipOperation;
                                rule["SkipValue"] = skipInt;
                            }
                            else
                            {
                                rule["UseSkip"] = false;
                            }

                            break;
                        }

                        case "float":
                        {
                            float targetFloat;
                            if (!float.TryParse(
                                targetValueText,
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out targetFloat))
                            {
                                System.Windows.Forms.MessageBox.Show(
                                    "Rule for '" + propName + "' has an invalid float TargetValue.",
                                    "Validation Error");
                                return;
                            }

                            rule["TargetValue"] = targetFloat;

                            if (useSkip)
                            {
                                if (skipValueText.Length == 0)
                                {
                                    System.Windows.Forms.MessageBox.Show(
                                        "Rule for '" + propName + "' has UseSkip enabled but no SkipValue.",
                                        "Validation Error");
                                    return;
                                }

                                float skipFloat;
                                if (!float.TryParse(
                                    skipValueText,
                                    System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out skipFloat))
                                {
                                    System.Windows.Forms.MessageBox.Show(
                                        "Rule for '" + propName + "' has an invalid float SkipValue.",
                                        "Validation Error");
                                    return;
                                }

                                rule["UseSkip"] = true;
                                rule["SkipOperation"] = skipOperation;
                                rule["SkipValue"] = skipFloat;
                            }
                            else
                            {
                                rule["UseSkip"] = false;
                            }

                            break;
                        }
                    }

                    tempRules.Add(rule);
                }

                if (tempRules.Count == 0)
                {
                    System.Windows.Forms.MessageBox.Show(
                        "Please enable at least one valid rule.",
                        "Validation Error");
                    return;
                }

                SaveCurrentConfig();

                entryNameConditions = tempConditions;
                useAndLogicForEntryName = tempUseAndLogic;
                parsedRules = tempRules;

                configForm.DialogResult = System.Windows.Forms.DialogResult.OK;
                configForm.Close();
            }
            catch (System.Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.ToString(), "Validation Error");
            }
        };

        var dialogResult = configForm.ShowDialog(form);
        if (dialogResult != System.Windows.Forms.DialogResult.OK)
            return;

        var propertyRuleMap =
            new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, object>>(
                System.StringComparer.OrdinalIgnoreCase);

        foreach (var rule in parsedRules)
        {
            string propName = rule["PropName"].ToString();
            propertyRuleMap[propName] = rule;
        }

        System.Windows.Forms.TreeNode exportDataNode = null;
        foreach (System.Windows.Forms.TreeNode node in tree.Nodes)
        {
            if (node.Text.StartsWith("Export Data"))
            {
                exportDataNode = node;
                break;
            }
        }

        if (exportDataNode == null)
        {
            System.Windows.Forms.MessageBox.Show("Could not find 'Export Data' node.", "Script Error");
            return;
        }

        SelectNode(exportDataNode);
        exportDataNode.Expand();

        var export1Node = FindChildNode(exportDataNode, "Export 1");
        if (export1Node == null)
        {
            System.Windows.Forms.MessageBox.Show("Could not find 'Export 1' node.", "Script Error");
            return;
        }

        SelectNode(export1Node);
        export1Node.Expand();

        System.Windows.Forms.TreeNode tableInfoNode = null;

        var dataTableNode = FindChildNode(export1Node, "DataTable");
        if (dataTableNode != null)
        {
            SelectNode(dataTableNode);
            dataTableNode.Expand();
            tableInfoNode = FindChildNode(dataTableNode, "Table Info");
        }

        if (tableInfoNode == null)
            tableInfoNode = FindChildNode(export1Node, "Table Info");

        if (tableInfoNode == null)
        {
            System.Windows.Forms.MessageBox.Show("Could not find 'Table Info' node under Export 1.", "Script Error");
            return;
        }

        SelectNode(tableInfoNode);
        tableInfoNode.Expand();

        int matchedEntries = 0;
        int editedEntries = 0;
        int editedValues = 0;
        int editedIsZeroFlags = 0;
        int skippedEntries = 0;

        foreach (System.Windows.Forms.TreeNode entryNode in tableInfoNode.Nodes)
        {
            string entryName = entryNode.Text ?? string.Empty;

            if (!EntryMatchesConditions(entryName, entryNameConditions, useAndLogicForEntryName))
            {
                skippedEntries++;
                continue;
            }

            matchedEntries++;
            SelectNode(entryNode);

            bool changedThisEntry = false;

            foreach (System.Windows.Forms.DataGridViewRow row in dataGridView.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Cells[nameColumnIndex] == null || row.Cells[valueColumnIndex] == null) continue;

                object propNameObject = row.Cells[nameColumnIndex].Value;
                if (propNameObject == null) continue;

                string propName = propNameObject.ToString().Trim();
                if (!propertyRuleMap.ContainsKey(propName)) continue;

                var rule = propertyRuleMap[propName];

                string type = rule["Type"].ToString().Trim().ToLowerInvariant();
                object currentValueObject = row.Cells[valueColumnIndex].Value;

                bool setIsZeroWhenNullLike =
                    rule.ContainsKey("SetIsZeroWhenNullLike") &&
                    rule["SetIsZeroWhenNullLike"] != null &&
                    System.Convert.ToBoolean(rule["SetIsZeroWhenNullLike"]);

                if (setIsZeroWhenNullLike && isZeroColumnIndex >= 0 && row.Cells[isZeroColumnIndex] != null)
                {
                    if (IsNullLikeByType(type, currentValueObject))
                    {
                        object currentIsZeroObject = row.Cells[isZeroColumnIndex].Value;
                        bool currentIsZero = false;
                        bool parsedCurrentIsZero;
                        if (TryParseBool(currentIsZeroObject, out parsedCurrentIsZero))
                            currentIsZero = parsedCurrentIsZero;

                        if (!currentIsZero)
                        {
                            row.Cells[isZeroColumnIndex].Value = "True";
                            editedIsZeroFlags++;
                            changedThisEntry = true;
                        }
                    }
                }

                bool useSkip =
                    rule.ContainsKey("UseSkip") &&
                    rule["UseSkip"] != null &&
                    System.Convert.ToBoolean(rule["UseSkip"]);

                switch (type)
                {
                    case "bool":
                    {
                        bool currentValue;
                        if (!TryParseBool(currentValueObject, out currentValue))
                            break;

                        if (useSkip)
                        {
                            bool skipValue = System.Convert.ToBoolean(rule["SkipValue"]);
                            if (currentValue == skipValue)
                                break;
                        }

                        bool targetValue = System.Convert.ToBoolean(rule["TargetValue"]);
                        if (currentValue != targetValue)
                        {
                            row.Cells[valueColumnIndex].Value = targetValue ? "True" : "False";
                            editedValues++;
                            changedThisEntry = true;
                        }

                        break;
                    }

                    case "string":
                    {
                        string currentValue = currentValueObject == null ? string.Empty : currentValueObject.ToString();

                        if (useSkip)
                        {
                            string skipValue = rule["SkipValue"].ToString();
                            if (string.Equals(currentValue, skipValue, System.StringComparison.OrdinalIgnoreCase))
                                break;
                        }

                        string targetValue = rule["TargetValue"].ToString();
                        if (!string.Equals(currentValue, targetValue, System.StringComparison.Ordinal))
                        {
                            row.Cells[valueColumnIndex].Value = targetValue;
                            editedValues++;
                            changedThisEntry = true;
                        }

                        break;
                    }

                    case "int":
                    {
                        int currentValue;
                        if (!TryParseInt(currentValueObject, out currentValue))
                            break;

                        if (useSkip)
                        {
                            string skipOperation = rule["SkipOperation"].ToString();
                            float skipValue = System.Convert.ToSingle(rule["SkipValue"], System.Globalization.CultureInfo.InvariantCulture);

                            if (EvaluateSkipNumeric(skipOperation, currentValue, skipValue))
                                break;
                        }

                        string targetOperation = rule["TargetOperation"].ToString();
                        int targetValue = System.Convert.ToInt32(rule["TargetValue"]);
                        int newValue = ApplyIntOperation(currentValue, targetOperation, targetValue);

                        if (newValue != currentValue)
                        {
                            row.Cells[valueColumnIndex].Value = newValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                            editedValues++;
                            changedThisEntry = true;
                        }

                        break;
                    }

                    case "float":
                    {
                        float currentValue;
                        if (!TryParseFloat(currentValueObject, out currentValue))
                            break;

                        if (useSkip)
                        {
                            string skipOperation = rule["SkipOperation"].ToString();
                            float skipValue = System.Convert.ToSingle(rule["SkipValue"], System.Globalization.CultureInfo.InvariantCulture);

                            if (EvaluateSkipNumeric(skipOperation, currentValue, skipValue))
                                break;
                        }

                        string targetOperation = rule["TargetOperation"].ToString();
                        float targetValue = System.Convert.ToSingle(rule["TargetValue"], System.Globalization.CultureInfo.InvariantCulture);
                        float newValue = ApplyFloatOperation(currentValue, targetOperation, targetValue);

                        if (System.Math.Abs(newValue - currentValue) >= 0.000001f)
                        {
                            row.Cells[valueColumnIndex].Value = FormatFloat(newValue);
                            editedValues++;
                            changedThisEntry = true;
                        }

                        break;
                    }
                }
            }

            if (changedThisEntry)
                editedEntries++;
        }

        string logicText = useAndLogicForEntryName ? "AND" : "OR";

        System.Windows.Forms.MessageBox.Show(
            "Done.\n\n" +
            "Config file:\n" + configFilePath + "\n\n" +
            "Entry logic: " + logicText + "\n" +
            "Entry conditions: " + (entryNameConditions.Count == 0 ? "(none)" : string.Join(", ", entryNameConditions)) + "\n" +
            "Active rules: " + propertyRuleMap.Count + "\n\n" +
            "Matched entries: " + matchedEntries + "\n" +
            "Edited entries: " + editedEntries + "\n" +
            "Edited values: " + editedValues + "\n" +
            "Edited Is Zero flags: " + editedIsZeroFlags + "\n" +
            "Skipped entries: " + skippedEntries,
            "Batch Edit Complete");
    }
    catch (System.Exception ex)
    {
        System.Windows.Forms.MessageBox.Show(ex.ToString(), "Script Error");
    }
});