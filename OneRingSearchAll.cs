UAGUtils.InvokeUI(() =>
{
    try
    {
        const float FloatEpsilon = 0.000001f;

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
                "Could not access required UI fields.\r\n\r\n" +
                "Missing field(s): " +
                (treeField == null ? "treeView1 " : "") +
                (dgvField == null ? "dataGridView1" : ""),
                "Script Error",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);
            return;
        }

        var tree = treeField.GetValue(form) as System.Windows.Forms.TreeView;
        var dataGridView = dgvField.GetValue(form) as System.Windows.Forms.DataGridView;

        if (tree == null || dataGridView == null)
        {
            System.Windows.Forms.MessageBox.Show(
                "Could not access the active tree or grid.\r\n\r\n" +
                "treeView1 is " + (tree == null ? "null" : "ok") + "\r\n" +
                "dataGridView1 is " + (dataGridView == null ? "null" : "ok"),
                "Script Error",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);
            return;
        }

        var originallySelectedNode = tree.SelectedNode;

        string configDirectory = System.Windows.Forms.Application.LocalUserAppDataPath;
        string configFilePath = System.IO.Path.Combine(configDirectory, "UAssetGUI_BatchRuleEditor_LastConfig.txt");


        System.Windows.Forms.TreeNode FindFirstNodeRecursive(
            System.Windows.Forms.TreeNodeCollection nodes,
            System.Func<System.Windows.Forms.TreeNode, bool> predicate)
        {
            foreach (System.Windows.Forms.TreeNode node in nodes)
            {
                if (predicate(node))
                    return node;

                var found = FindFirstNodeRecursive(node.Nodes, predicate);
                if (found != null)
                    return found;
            }

            return null;
        }

        void CollectNodesRecursive(
            System.Windows.Forms.TreeNode root,
            System.Func<System.Windows.Forms.TreeNode, bool> predicate,
            System.Collections.Generic.List<System.Windows.Forms.TreeNode> results)
        {
            if (root == null)
                return;

            foreach (System.Windows.Forms.TreeNode child in root.Nodes)
            {
                if (predicate(child))
                    results.Add(child);

                CollectNodesRecursive(child, predicate, results);
            }
        }

        System.Collections.Generic.List<System.Windows.Forms.TreeNode> EnumerateNodeAndDescendants(
            System.Windows.Forms.TreeNode root)
        {
            var results = new System.Collections.Generic.List<System.Windows.Forms.TreeNode>();
            if (root == null)
                return results;

            results.Add(root);

            foreach (System.Windows.Forms.TreeNode child in root.Nodes)
            {
                var childResults = EnumerateNodeAndDescendants(child);
                foreach (var item in childResults)
                    results.Add(item);
            }

            return results;
        }

        void SelectNode(System.Windows.Forms.TreeNode node)
        {
            if (node == null)
                return;

            var current = node.Parent;
            while (current != null)
            {
                current.Expand();
                current = current.Parent;
            }

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

        bool MatchesConditions(
            string sourceText,
            System.Collections.Generic.List<string> conditions,
            bool useAndLogic)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
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
                    if (!ContainsToken(sourceText, condition))
                        return false;
                }

                return true;
            }

            foreach (string condition in filteredConditions)
            {
                if (ContainsToken(sourceText, condition))
                    return true;
            }

            return false;
        }

        bool DescendantNameMatches(
            System.Windows.Forms.TreeNode root,
            System.Collections.Generic.List<string> conditions,
            bool useAndLogic)
        {
            if (root == null)
                return false;

            foreach (System.Windows.Forms.TreeNode child in root.Nodes)
            {
                if (MatchesConditions(child.Text ?? string.Empty, conditions, useAndLogic))
                    return true;

                if (DescendantNameMatches(child, conditions, useAndLogic))
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
                        return TryParseFloat(value, out parsed) && System.Math.Abs(parsed) < FloatEpsilon;
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
                case "eq": return System.Math.Abs(currentValue - skipValue) < FloatEpsilon;
                case "lt": return currentValue < skipValue;
                case "gt": return currentValue > skipValue;
                case "lte": return currentValue <= skipValue;
                case "gte": return currentValue >= skipValue;
                default: return false;
            }
        }

        bool IsValidNumericSkipOperation(string operation)
        {
            switch ((operation ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "eq":
                case "lt":
                case "gt":
                case "lte":
                case "gte":
                    return true;
                default:
                    return false;
            }
        }

        bool IsValidTargetOperation(string operation)
        {
            switch ((operation ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "set":
                case "add":
                case "sub":
                case "mul":
                case "div":
                    return true;
                default:
                    return false;
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
                case "div": return System.Math.Abs(targetValue) < FloatEpsilon ? currentValue : currentValue / targetValue;
                default: return currentValue;
            }
        }

        bool IsExportEntryNode(System.Windows.Forms.TreeNode node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Text))
                return false;

            string text = node.Text.Trim();

            if (!text.StartsWith("Export ", System.StringComparison.OrdinalIgnoreCase))
                return false;

            if (text.Equals("Export Data", System.StringComparison.OrdinalIgnoreCase))
                return false;

            int firstParen = text.IndexOf('(');
            int lastParen = text.LastIndexOf(')');

            if (firstParen > 0 && lastParen > firstParen)
                return true;

            var parts = text.Split(' ');
            if (parts.Length >= 2)
            {
                int exportNumber;
                if (int.TryParse(parts[1], out exportNumber))
                    return true;
            }

            return false;
        }

        System.Collections.Generic.List<string> ReadNonEmptyLines(System.Windows.Forms.TextBox textBox)
        {
            var results = new System.Collections.Generic.List<string>();

            if (textBox == null)
                return results;

            foreach (string line in textBox.Lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    results.Add(line.Trim());
            }

            return results;
        }

        bool IsStructuralDetailNode(System.Windows.Forms.TreeNode node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Text))
                return false;

            string text = node.Text.Trim();

            return
                text.StartsWith("BlueprintGeneratedClass", System.StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("UStruct Data", System.StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("UClass Data", System.StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("Extra Data", System.StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("DataTable", System.StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("Table Info", System.StringComparison.OrdinalIgnoreCase);
        }

        System.Collections.Generic.List<System.Windows.Forms.TreeNode> GetProcessableEntryNodes(System.Windows.Forms.TreeView sourceTree)
        {
            var results = new System.Collections.Generic.List<System.Windows.Forms.TreeNode>();

            var exportDataNode = FindFirstNodeRecursive(
                sourceTree.Nodes,
                n => n.Text != null && n.Text.StartsWith("Export Data", System.StringComparison.OrdinalIgnoreCase));

            if (exportDataNode == null)
                return results;

            SelectNode(exportDataNode);
            exportDataNode.Expand();

            var tableInfoNode = FindFirstNodeRecursive(
                exportDataNode.Nodes,
                n => n.Text != null && n.Text.StartsWith("Table Info", System.StringComparison.OrdinalIgnoreCase));

            if (tableInfoNode != null && tableInfoNode.Nodes.Count > 0)
            {
                SelectNode(tableInfoNode);
                tableInfoNode.Expand();

                foreach (System.Windows.Forms.TreeNode child in tableInfoNode.Nodes)
                {
                    if (!IsStructuralDetailNode(child))
                        results.Add(child);
                }

                if (results.Count > 0)
                    return results;
            }

            CollectNodesRecursive(
                exportDataNode,
                n => IsExportEntryNode(n),
                results);

            var deduped = new System.Collections.Generic.List<System.Windows.Forms.TreeNode>();
            var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var node in results)
            {
                string key = node.FullPath;
                if (seen.Add(key))
                    deduped.Add(node);
            }

            return deduped;
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

        System.Collections.Generic.Dictionary<string, int> BuildColumnMap(System.Windows.Forms.DataGridView grid)
        {
            var map = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < grid.Columns.Count; i++)
            {
                var column = grid.Columns[i];

                if (!string.IsNullOrWhiteSpace(column.HeaderText) && !map.ContainsKey(column.HeaderText))
                    map[column.HeaderText] = i;

                if (!string.IsNullOrWhiteSpace(column.Name) && !map.ContainsKey(column.Name))
                    map[column.Name] = i;
            }

            return map;
        }

        string SafeCellText(System.Windows.Forms.DataGridViewRow row, int index)
        {
            if (row == null) return string.Empty;
            if (index < 0 || index >= row.Cells.Count) return string.Empty;
            if (row.Cells[index] == null) return string.Empty;

            object value = row.Cells[index].Value;
            return value == null ? string.Empty : value.ToString();
        }

        string BuildPathRelativeToEntry(System.Windows.Forms.TreeNode entryNode, System.Windows.Forms.TreeNode matchedNode)
        {
            if (entryNode == null || matchedNode == null)
                return string.Empty;

            if (matchedNode == entryNode)
                return "(entry root)";

            var segments = new System.Collections.Generic.List<string>();
            var current = matchedNode;

            while (current != null && current != entryNode)
            {
                segments.Insert(0, current.Text);
                current = current.Parent;
            }

            return segments.Count == 0 ? "(entry root)" : string.Join(" -> ", segments.ToArray());
        }

        string BuildManualNodePath(System.Windows.Forms.TreeNode node)
        {
            if (node == null)
                return string.Empty;

            var parts = new System.Collections.Generic.List<string>();
            var current = node;

            while (current != null)
            {
                parts.Insert(0, current.Text);
                current = current.Parent;
            }

            return string.Join(" -> ", parts.ToArray());
        }

        System.Windows.Forms.TreeNode FindNodeByManualPath(System.Windows.Forms.TreeView sourceTree, string manualPath)
        {
            if (sourceTree == null || string.IsNullOrWhiteSpace(manualPath))
                return null;

            foreach (System.Windows.Forms.TreeNode root in sourceTree.Nodes)
            {
                var found = FindNodeByManualPathRecursive(root, manualPath);
                if (found != null)
                    return found;
            }

            return null;
        }

        System.Windows.Forms.TreeNode FindNodeByManualPathRecursive(System.Windows.Forms.TreeNode node, string manualPath)
        {
            if (node == null)
                return null;

            if (string.Equals(BuildManualNodePath(node), manualPath, System.StringComparison.Ordinal))
                return node;

            foreach (System.Windows.Forms.TreeNode child in node.Nodes)
            {
                var found = FindNodeByManualPathRecursive(child, manualPath);
                if (found != null)
                    return found;
            }

            return null;
        }

        bool GridContainsAnyTargetProperty(
            System.Windows.Forms.DataGridView grid,
            int nameColumnIndex,
            System.Collections.Generic.ICollection<string> propertyNames)
        {
            if (grid == null || nameColumnIndex < 0 || propertyNames == null || propertyNames.Count == 0)
                return false;

            foreach (System.Windows.Forms.DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Cells[nameColumnIndex] == null) continue;

                object nameObj = row.Cells[nameColumnIndex].Value;
                if (nameObj == null) continue;

                string rowName = nameObj.ToString().Trim();
                if (propertyNames.Contains(rowName))
                    return true;
            }

            return false;
        }

        System.Windows.Forms.TreeNode ResolveBestEditableNode(
            System.Windows.Forms.TreeNode entryNode,
            System.Windows.Forms.DataGridView grid,
            int nameColumnIndex,
            System.Collections.Generic.ICollection<string> propertyNames)
        {
            var candidates = EnumerateNodeAndDescendants(entryNode);

            foreach (var candidate in candidates)
            {
                SelectNode(candidate);

                if (GridContainsAnyTargetProperty(grid, nameColumnIndex, propertyNames))
                    return candidate;
            }

            return entryNode;
        }

        void SearchSelectedNodePropertyGrid(
            System.Windows.Forms.TreeNode entryNode,
            System.Windows.Forms.TreeNode currentNode,
            string searchTerm,
            System.Windows.Forms.DataGridView resultsGrid,
            System.Collections.Generic.HashSet<string> dedupeKeys,
            ref int totalHits)
        {
            var columnMap = BuildColumnMap(dataGridView);

            int nameCol = columnMap.ContainsKey("Name") ? columnMap["Name"] : -1;
            int valueCol = columnMap.ContainsKey("Value") ? columnMap["Value"] : -1;

            foreach (System.Windows.Forms.DataGridViewRow row in dataGridView.Rows)
            {
                if (row.IsNewRow) continue;

                string propName = SafeCellText(row, nameCol).Trim();
                string propValue = SafeCellText(row, valueCol).Trim();

                bool matched = false;
                string matchedSource = string.Empty;
                string matchedText = string.Empty;

                if (nameCol >= 0 && ContainsToken(propName, searchTerm))
                {
                    matched = true;
                    matchedSource = "Property Name";
                    matchedText = propName;
                }

                if (!matched && valueCol >= 0 && ContainsToken(propValue, searchTerm))
                {
                    matched = true;
                    matchedSource = "Property Value";
                    matchedText = propValue;
                }

                if (!matched)
                {
                    for (int cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
                    {
                        if (cellIndex == nameCol || cellIndex == valueCol)
                            continue;

                        string cellText = SafeCellText(row, cellIndex).Trim();
                        if (!ContainsToken(cellText, searchTerm))
                            continue;

                        string header = cellIndex >= 0 && cellIndex < dataGridView.Columns.Count
                            ? dataGridView.Columns[cellIndex].HeaderText
                            : "Column " + cellIndex;

                        matched = true;
                        matchedSource = string.IsNullOrWhiteSpace(header) ? "Other Column" : header;
                        matchedText = cellText;
                        break;
                    }
                }

                if (!matched)
                    continue;

                string relativePath = BuildPathRelativeToEntry(entryNode, currentNode);
                string manualPath = BuildManualNodePath(currentNode);
                string dedupeKey =
                    searchTerm + "\n" +
                    manualPath + "\n" +
                    matchedSource + "\n" +
                    propName + "\n" +
                    matchedText;

                if (!dedupeKeys.Add(dedupeKey))
                    continue;

                resultsGrid.Rows.Add(
                    false,
                    searchTerm,
                    entryNode.Text,
                    currentNode.Text,
                    relativePath,
                    matchedSource,
                    propName,
                    matchedText,
                    manualPath);

                totalHits++;
            }
        }

        void SearchNodeAndChildren(
            System.Windows.Forms.TreeNode currentNode,
            System.Windows.Forms.TreeNode entryNode,
            System.Collections.Generic.List<string> searchTerms,
            System.Windows.Forms.DataGridView resultsGrid,
            System.Collections.Generic.HashSet<string> dedupeKeys,
            ref int totalHits)
        {
            if (currentNode == null)
                return;

            SelectNode(currentNode);

            foreach (string searchTerm in searchTerms)
            {
                if (ContainsToken(currentNode.Text, searchTerm))
                {
                    string relativePath = BuildPathRelativeToEntry(entryNode, currentNode);
                    string manualPath = BuildManualNodePath(currentNode);
                    string dedupeKey =
                        searchTerm + "\n" +
                        manualPath + "\n" +
                        "Tree Node" + "\n" +
                        currentNode.Text;

                    if (dedupeKeys.Add(dedupeKey))
                    {
                        resultsGrid.Rows.Add(
                            false,
                            searchTerm,
                            entryNode.Text,
                            currentNode.Text,
                            relativePath,
                            "Tree Node",
                            "",
                            currentNode.Text,
                            manualPath);

                        totalHits++;
                    }
                }

                SearchSelectedNodePropertyGrid(
                    entryNode,
                    currentNode,
                    searchTerm,
                    resultsGrid,
                    dedupeKeys,
                    ref totalHits);
            }

            foreach (System.Windows.Forms.TreeNode child in currentNode.Nodes)
                SearchNodeAndChildren(child, entryNode, searchTerms, resultsGrid, dedupeKeys, ref totalHits);
        }

        int nameColumnIndex = -1;
        int valueColumnIndex = -1;
        int isZeroColumnIndex = -1;

        for (int columnIndex = 0; columnIndex < dataGridView.Columns.Count; columnIndex++)
        {
            string headerText = dataGridView.Columns[columnIndex].HeaderText ?? string.Empty;
            if (headerText == "Name" || headerText == "Property Name") nameColumnIndex = columnIndex;
            if (headerText == "Value") valueColumnIndex = columnIndex;
            if (headerText == "Is Zero") isZeroColumnIndex = columnIndex;
        }

        if (nameColumnIndex < 0 || valueColumnIndex < 0)
        {
            System.Windows.Forms.MessageBox.Show(
                "Could not find required grid columns.\r\n\r\n" +
                "Name column found: " + (nameColumnIndex >= 0 ? "Yes" : "No") + "\r\n" +
                "Value column found: " + (valueColumnIndex >= 0 ? "Yes" : "No"),
                "Script Error",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);
            return;
        }

        bool runAgain = true;
        string lastStatusText = "Ready.";

        while (runAgain)
        {
            runAgain = false;

            var configForm = new System.Windows.Forms.Form();
            configForm.Text = "Batch Rule Editor Studio";
            configForm.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            configForm.Width = 1420;
            configForm.Height = 1040;
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
            filtersTextBox.AcceptsReturn = true;
            filtersTextBox.AcceptsTab = false;
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

            var selectedOnlyCheckBox = new System.Windows.Forms.CheckBox();
            selectedOnlyCheckBox.Text = "Only process currently selected entry";
            selectedOnlyCheckBox.Left = 430;
            selectedOnlyCheckBox.Top = 34;
            selectedOnlyCheckBox.Width = 260;

            var recursiveChildFilterCheckBox = new System.Windows.Forms.CheckBox();
            recursiveChildFilterCheckBox.Text = "Enable recursive child-name filter";
            recursiveChildFilterCheckBox.Left = 12;
            recursiveChildFilterCheckBox.Top = 132;
            recursiveChildFilterCheckBox.Width = 260;

            var childFiltersLabel = new System.Windows.Forms.Label();
            childFiltersLabel.Text = "Child/descendant name conditions (one per line):";
            childFiltersLabel.Left = 12;
            childFiltersLabel.Top = 158;
            childFiltersLabel.Width = 300;

            var childFiltersTextBox = new System.Windows.Forms.TextBox();
            childFiltersTextBox.Left = 12;
            childFiltersTextBox.Top = 180;
            childFiltersTextBox.Width = 260;
            childFiltersTextBox.Height = 90;
            childFiltersTextBox.Multiline = true;
            childFiltersTextBox.AcceptsReturn = true;
            childFiltersTextBox.AcceptsTab = false;
            childFiltersTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;

            var childLogicLabel = new System.Windows.Forms.Label();
            childLogicLabel.Text = "Child condition logic:";
            childLogicLabel.Left = 290;
            childLogicLabel.Top = 158;
            childLogicLabel.Width = 120;

            var childLogicComboBox = new System.Windows.Forms.ComboBox();
            childLogicComboBox.Left = 290;
            childLogicComboBox.Top = 180;
            childLogicComboBox.Width = 120;
            childLogicComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            childLogicComboBox.Items.Add("AND");
            childLogicComboBox.Items.Add("OR");
            childLogicComboBox.SelectedIndex = 0;

            void UpdateChildFilterControlState()
            {
                bool enabled = recursiveChildFilterCheckBox.Checked;
                childFiltersLabel.Enabled = enabled;
                childFiltersTextBox.Enabled = enabled;
                childLogicLabel.Enabled = enabled;
                childLogicComboBox.Enabled = enabled;
            }

            recursiveChildFilterCheckBox.CheckedChanged += (sender, args) =>
            {
                UpdateChildFilterControlState();
            };

            var helpLabel = new System.Windows.Forms.Label();
            helpLabel.Left = 430;
            helpLabel.Top = 12;
            helpLabel.Width = 960;
            helpLabel.Height = 120;
            helpLabel.Text =
                "Workspace tips\r\n" +
                "• Ctrl+Enter runs the current batch\r\n" +
                "• Config is auto-saved while you work\r\n" +
                "• Find Targets discovers properties and entry names\r\n" +
                "• Double-click a hit to jump directly to the source node";

            var searchTermsLabel = new System.Windows.Forms.Label();
            searchTermsLabel.Text = "Find Targets terms (one per line):";
            searchTermsLabel.Left = 430;
            searchTermsLabel.Top = 132;
            searchTermsLabel.Width = 250;

            var searchTermsTextBox = new System.Windows.Forms.TextBox();
            searchTermsTextBox.Left = 430;
            searchTermsTextBox.Top = 154;
            searchTermsTextBox.Width = 260;
            searchTermsTextBox.Height = 116;
            searchTermsTextBox.Multiline = true;
            searchTermsTextBox.AcceptsReturn = true;
            searchTermsTextBox.AcceptsTab = false;
            searchTermsTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            searchTermsTextBox.Text = "AvailableParry" + System.Environment.NewLine + "CoolTime";

            var searchSelectedOnlyCheckBox = new System.Windows.Forms.CheckBox();
            searchSelectedOnlyCheckBox.Text = "Search only currently selected entry";
            searchSelectedOnlyCheckBox.Left = 708;
            searchSelectedOnlyCheckBox.Top = 154;
            searchSelectedOnlyCheckBox.Width = 250;

            var useSearchHitsAsScopeCheckBox = new System.Windows.Forms.CheckBox();
            useSearchHitsAsScopeCheckBox.Text = "Run only on entries found in search hits";
            useSearchHitsAsScopeCheckBox.Left = 708;
            useSearchHitsAsScopeCheckBox.Top = 180;
            useSearchHitsAsScopeCheckBox.Width = 280;

            var autoAddSearchPropsCheckBox = new System.Windows.Forms.CheckBox();
            autoAddSearchPropsCheckBox.Text = "Add selected search-hit properties as rules";
            autoAddSearchPropsCheckBox.Left = 708;
            autoAddSearchPropsCheckBox.Top = 206;
            autoAddSearchPropsCheckBox.Width = 300;

            var autoAddSearchEntriesCheckBox = new System.Windows.Forms.CheckBox();
            autoAddSearchEntriesCheckBox.Text = "Add selected search-hit entries to entry filters";
            autoAddSearchEntriesCheckBox.Left = 708;
            autoAddSearchEntriesCheckBox.Top = 232;
            autoAddSearchEntriesCheckBox.Width = 310;

            var searchButton = new System.Windows.Forms.Button();
            searchButton.Text = "Scan Targets";
            searchButton.Left = 1060;
            searchButton.Top = 154;
            searchButton.Width = 140;

            var clearSearchResultsButton = new System.Windows.Forms.Button();
            clearSearchResultsButton.Text = "Clear Results";
            clearSearchResultsButton.Left = 1210;
            clearSearchResultsButton.Top = 154;
            clearSearchResultsButton.Width = 110;

            var addSelectedPropsButton = new System.Windows.Forms.Button();
            addSelectedPropsButton.Text = "Promote Props to Rules";
            addSelectedPropsButton.Left = 1060;
            addSelectedPropsButton.Top = 190;
            addSelectedPropsButton.Width = 140;

            var addSelectedEntriesButton = new System.Windows.Forms.Button();
            addSelectedEntriesButton.Text = "Promote Entries to Filters";
            addSelectedEntriesButton.Left = 1210;
            addSelectedEntriesButton.Top = 190;
            addSelectedEntriesButton.Width = 140;

            var searchResultsGrid = new System.Windows.Forms.DataGridView();
            searchResultsGrid.Left = 12;
            searchResultsGrid.Top = 286;
            searchResultsGrid.Width = 1390;
            searchResultsGrid.Height = 260;
            searchResultsGrid.Anchor =
                System.Windows.Forms.AnchorStyles.Top |
                System.Windows.Forms.AnchorStyles.Left |
                System.Windows.Forms.AnchorStyles.Right;
            searchResultsGrid.AllowUserToAddRows = false;
            searchResultsGrid.AllowUserToDeleteRows = false;
            searchResultsGrid.AllowUserToResizeRows = false;
            searchResultsGrid.RowHeadersVisible = false;
            searchResultsGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            searchResultsGrid.MultiSelect = true;
            searchResultsGrid.ReadOnly = false;
            searchResultsGrid.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            searchResultsGrid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.None;

            var searchSelectColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            searchSelectColumn.Name = "UseHit";
            searchSelectColumn.HeaderText = "Use";
            searchSelectColumn.Width = 45;

            var termColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            termColumn.Name = "SearchTerm";
            termColumn.HeaderText = "SearchTerm";
            termColumn.Width = 120;
            termColumn.ReadOnly = true;

            var entryColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            entryColumn.Name = "ParentEntry";
            entryColumn.HeaderText = "ParentEntry";
            entryColumn.Width = 210;
            entryColumn.ReadOnly = true;

            var nodeColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            nodeColumn.Name = "MatchedNode";
            nodeColumn.HeaderText = "MatchedNode";
            nodeColumn.Width = 200;
            nodeColumn.ReadOnly = true;

            var relativePathColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            relativePathColumn.Name = "PathWithinEntry";
            relativePathColumn.HeaderText = "PathWithinEntry";
            relativePathColumn.Width = 260;
            relativePathColumn.ReadOnly = true;

            var sourceColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            sourceColumn.Name = "MatchedSource";
            sourceColumn.HeaderText = "MatchedSource";
            sourceColumn.Width = 120;
            sourceColumn.ReadOnly = true;

            var propColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            propColumn.Name = "PropName";
            propColumn.HeaderText = "PropName";
            propColumn.Width = 160;
            propColumn.ReadOnly = true;

            var textColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            textColumn.Name = "MatchedText";
            textColumn.HeaderText = "MatchedText";
            textColumn.Width = 220;
            textColumn.ReadOnly = true;

            var manualPathColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            manualPathColumn.Name = "ManualPath";
            manualPathColumn.HeaderText = "ManualPath";
            manualPathColumn.Width = 400;
            manualPathColumn.ReadOnly = true;

            searchResultsGrid.Columns.Add(searchSelectColumn);
            searchResultsGrid.Columns.Add(termColumn);
            searchResultsGrid.Columns.Add(entryColumn);
            searchResultsGrid.Columns.Add(nodeColumn);
            searchResultsGrid.Columns.Add(relativePathColumn);
            searchResultsGrid.Columns.Add(sourceColumn);
            searchResultsGrid.Columns.Add(propColumn);
            searchResultsGrid.Columns.Add(textColumn);
            searchResultsGrid.Columns.Add(manualPathColumn);

            searchResultsGrid.CurrentCellDirtyStateChanged += (sender, args) =>
            {
                if (searchResultsGrid.IsCurrentCellDirty)
                    searchResultsGrid.CommitEdit(System.Windows.Forms.DataGridViewDataErrorContexts.Commit);
            };

            searchResultsGrid.CellDoubleClick += (sender, args) =>
            {
                if (args.RowIndex < 0)
                    return;

                string manualPath = SafeCellText(searchResultsGrid.Rows[args.RowIndex], searchResultsGrid.Columns["ManualPath"].Index);
                var targetNode = FindNodeByManualPath(tree, manualPath);
                if (targetNode != null)
                    SelectNode(targetNode);
            };

            var rulesGrid = new System.Windows.Forms.DataGridView();
            rulesGrid.Left = 12;
            rulesGrid.Top = 530;
            rulesGrid.Width = 1390;
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
            propNameColumn.Width = 190;

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
                    childFiltersTextBox.Clear();
                    searchTermsTextBox.Clear();
                    searchResultsGrid.Rows.Clear();
                    rulesGrid.Rows.Clear();
                    logicComboBox.SelectedItem = "AND";
                    childLogicComboBox.SelectedItem = "AND";
                    recursiveChildFilterCheckBox.Checked = false;
                    selectedOnlyCheckBox.Checked = false;
                    searchSelectedOnlyCheckBox.Checked = false;
                    useSearchHitsAsScopeCheckBox.Checked = false;
                    autoAddSearchPropsCheckBox.Checked = false;
                    autoAddSearchEntriesCheckBox.Checked = false;

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
                        else if (rawLine.StartsWith("SELECTED_ONLY|"))
                        {
                            var parts = SplitEscapedPipe(rawLine);
                            if (parts.Length >= 2)
                            {
                                bool enabled;
                                if (TryParseBool(UnescapeConfig(parts[1]), out enabled))
                                    selectedOnlyCheckBox.Checked = enabled;
                            }
                        }
                        else if (rawLine.StartsWith("CHILD_FILTER_ENABLED|"))
                        {
                            var parts = SplitEscapedPipe(rawLine);
                            if (parts.Length >= 2)
                            {
                                bool enabled;
                                if (TryParseBool(UnescapeConfig(parts[1]), out enabled))
                                    recursiveChildFilterCheckBox.Checked = enabled;
                            }
                        }
                        else if (rawLine.StartsWith("CHILD_LOGIC|"))
                        {
                            var parts = SplitEscapedPipe(rawLine);
                            if (parts.Length >= 2)
                            {
                                string logic = UnescapeConfig(parts[1]).Trim().ToUpperInvariant();
                                childLogicComboBox.SelectedItem = logic == "OR" ? "OR" : "AND";
                            }
                        }
                        else if (rawLine.StartsWith("CHILD_FILTER|"))
                        {
                            var parts = SplitEscapedPipe(rawLine);
                            if (parts.Length >= 2)
                            {
                                string filterValue = UnescapeConfig(parts[1]);
                                if (childFiltersTextBox.TextLength > 0)
                                    childFiltersTextBox.AppendText(System.Environment.NewLine);

                                childFiltersTextBox.AppendText(filterValue);
                            }
                        }
                        else if (rawLine.StartsWith("SEARCH_TERM|"))
                        {
                            var parts = SplitEscapedPipe(rawLine);
                            if (parts.Length >= 2)
                            {
                                string value = UnescapeConfig(parts[1]);
                                if (searchTermsTextBox.TextLength > 0)
                                    searchTermsTextBox.AppendText(System.Environment.NewLine);

                                searchTermsTextBox.AppendText(value);
                            }
                        }
                        else if (rawLine.StartsWith("SEARCH_SELECTED_ONLY|"))
                        {
                            var parts = SplitEscapedPipe(rawLine);
                            if (parts.Length >= 2)
                            {
                                bool enabled;
                                if (TryParseBool(UnescapeConfig(parts[1]), out enabled))
                                    searchSelectedOnlyCheckBox.Checked = enabled;
                            }
                        }
                        else if (rawLine.StartsWith("USE_SEARCH_HITS_SCOPE|"))
                        {
                            var parts = SplitEscapedPipe(rawLine);
                            if (parts.Length >= 2)
                            {
                                bool enabled;
                                if (TryParseBool(UnescapeConfig(parts[1]), out enabled))
                                    useSearchHitsAsScopeCheckBox.Checked = enabled;
                            }
                        }
                        else if (rawLine.StartsWith("AUTO_ADD_SEARCH_PROPS|"))
                        {
                            var parts = SplitEscapedPipe(rawLine);
                            if (parts.Length >= 2)
                            {
                                bool enabled;
                                if (TryParseBool(UnescapeConfig(parts[1]), out enabled))
                                    autoAddSearchPropsCheckBox.Checked = enabled;
                            }
                        }
                        else if (rawLine.StartsWith("AUTO_ADD_SEARCH_ENTRIES|"))
                        {
                            var parts = SplitEscapedPipe(rawLine);
                            if (parts.Length >= 2)
                            {
                                bool enabled;
                                if (TryParseBool(UnescapeConfig(parts[1]), out enabled))
                                    autoAddSearchEntriesCheckBox.Checked = enabled;
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

                    UpdateChildFilterControlState();
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

                lines.Add("SELECTED_ONLY|" + EscapeConfig(selectedOnlyCheckBox.Checked ? "True" : "False"));
                lines.Add("CHILD_FILTER_ENABLED|" + EscapeConfig(recursiveChildFilterCheckBox.Checked ? "True" : "False"));

                string childLogicValue = childLogicComboBox.SelectedItem == null ? "AND" : childLogicComboBox.SelectedItem.ToString();
                lines.Add("CHILD_LOGIC|" + EscapeConfig(childLogicValue));

                foreach (string filterLine in childFiltersTextBox.Lines)
                {
                    if (string.IsNullOrWhiteSpace(filterLine))
                        continue;

                    lines.Add("CHILD_FILTER|" + EscapeConfig(filterLine.Trim()));
                }

                foreach (string searchLine in searchTermsTextBox.Lines)
                {
                    if (string.IsNullOrWhiteSpace(searchLine))
                        continue;

                    lines.Add("SEARCH_TERM|" + EscapeConfig(searchLine.Trim()));
                }

                lines.Add("SEARCH_SELECTED_ONLY|" + EscapeConfig(searchSelectedOnlyCheckBox.Checked ? "True" : "False"));
                lines.Add("USE_SEARCH_HITS_SCOPE|" + EscapeConfig(useSearchHitsAsScopeCheckBox.Checked ? "True" : "False"));
                lines.Add("AUTO_ADD_SEARCH_PROPS|" + EscapeConfig(autoAddSearchPropsCheckBox.Checked ? "True" : "False"));
                lines.Add("AUTO_ADD_SEARCH_ENTRIES|" + EscapeConfig(autoAddSearchEntriesCheckBox.Checked ? "True" : "False"));

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

            UpdateChildFilterControlState();

            void AddUniqueEntryFilter(string entryName)
            {
                if (string.IsNullOrWhiteSpace(entryName))
                    return;

                var existing = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                foreach (string line in filtersTextBox.Lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        existing.Add(line.Trim());
                }

                if (existing.Contains(entryName.Trim()))
                    return;

                if (filtersTextBox.TextLength > 0)
                    filtersTextBox.AppendText(System.Environment.NewLine);

                filtersTextBox.AppendText(entryName.Trim());
            }

            void AddUniqueRuleForProperty(string propName)
            {
                if (string.IsNullOrWhiteSpace(propName))
                    return;

                foreach (System.Windows.Forms.DataGridViewRow row in rulesGrid.Rows)
                {
                    if (row.IsNewRow) continue;
                    string existing = row.Cells["PropName"].Value == null ? string.Empty : row.Cells["PropName"].Value.ToString().Trim();
                    if (string.Equals(existing, propName.Trim(), System.StringComparison.OrdinalIgnoreCase))
                        return;
                }

                AddRuleRow(true, propName.Trim(), "bool", "set", "true", false, "eq", "", true);
            }

            void AddCheckedSearchHitsToRules()
            {
                foreach (System.Windows.Forms.DataGridViewRow row in searchResultsGrid.Rows)
                {
                    if (row.IsNewRow) continue;

                    bool useHit = false;
                    object checkedObj = row.Cells["UseHit"].Value;
                    bool parsedBool;
                    if (TryParseBool(checkedObj, out parsedBool))
                        useHit = parsedBool;

                    if (!useHit)
                        continue;

                    string propName = SafeCellText(row, searchResultsGrid.Columns["PropName"].Index).Trim();
                    if (!string.IsNullOrWhiteSpace(propName))
                        AddUniqueRuleForProperty(propName);
                }
            }

            void AddCheckedSearchHitsToEntryFilters()
            {
                foreach (System.Windows.Forms.DataGridViewRow row in searchResultsGrid.Rows)
                {
                    if (row.IsNewRow) continue;

                    bool useHit = false;
                    object checkedObj = row.Cells["UseHit"].Value;
                    bool parsedBool;
                    if (TryParseBool(checkedObj, out parsedBool))
                        useHit = parsedBool;

                    if (!useHit)
                        continue;

                    string entryName = SafeCellText(row, searchResultsGrid.Columns["ParentEntry"].Index).Trim();
                    if (!string.IsNullOrWhiteSpace(entryName))
                        AddUniqueEntryFilter(entryName);
                }
            }

            var statusLabel = new System.Windows.Forms.Label();
            statusLabel.Text = "Status:";
            statusLabel.Left = 12;
            statusLabel.Top = 790;
            statusLabel.Width = 80;
            statusLabel.Anchor =
                System.Windows.Forms.AnchorStyles.Left |
                System.Windows.Forms.AnchorStyles.Bottom;

            var statusTextBox = new System.Windows.Forms.TextBox();
            statusTextBox.Left = 12;
            statusTextBox.Top = 812;
            statusTextBox.Width = 1390;
            statusTextBox.Height = 170;
            statusTextBox.Multiline = true;
            statusTextBox.ReadOnly = true;
            statusTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            statusTextBox.Anchor =
                System.Windows.Forms.AnchorStyles.Left |
                System.Windows.Forms.AnchorStyles.Right |
                System.Windows.Forms.AnchorStyles.Bottom;
            statusTextBox.Text = lastStatusText;

            searchButton.Click += (sender, args) =>
            {
                try
                {
                    searchResultsGrid.Rows.Clear();

                    var searchTerms = ReadNonEmptyLines(searchTermsTextBox);
                    if (searchTerms.Count == 0)
                        throw new System.Exception("No search terms were provided.");

                    var entryNameConditions = ReadNonEmptyLines(filtersTextBox);
                    bool useAndLogicForEntryName =
                        logicComboBox.SelectedItem == null ||
                        logicComboBox.SelectedItem.ToString().Equals("AND", System.StringComparison.OrdinalIgnoreCase);

                    bool useRecursiveChildFilter = recursiveChildFilterCheckBox.Checked;
                    var childNameConditions = ReadNonEmptyLines(childFiltersTextBox);
                    bool useAndLogicForChildName =
                        childLogicComboBox.SelectedItem == null ||
                        childLogicComboBox.SelectedItem.ToString().Equals("AND", System.StringComparison.OrdinalIgnoreCase);

                    if (useRecursiveChildFilter && childNameConditions.Count == 0)
                    {
                        throw new System.Exception(
                            "Recursive child-name filter is enabled, but no child conditions were entered.");
                    }

                    var dedupeKeys =
                        new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

                    int totalHits = 0;
                    int skippedByEntryFilter = 0;
                    int skippedByChildFilter = 0;

                    var entryNodes = new System.Collections.Generic.List<System.Windows.Forms.TreeNode>();

                    if (searchSelectedOnlyCheckBox.Checked)
                    {
                        if (originallySelectedNode == null)
                        {
                            throw new System.Exception(
                                "Search-selected-only mode is enabled, but there was no selected node when the script started.");
                        }

                        entryNodes.Add(originallySelectedNode);
                    }
                    else
                    {
                        entryNodes = GetProcessableEntryNodes(tree);

                        if (entryNodes.Count == 0)
                        {
                            throw new System.Exception("No processable entry nodes were found for search.");
                        }
                    }

                    int searchedEntries = 0;

                    foreach (System.Windows.Forms.TreeNode entryNode in entryNodes)
                    {
                        string entryName = entryNode.Text ?? string.Empty;

                        if (!searchSelectedOnlyCheckBox.Checked)
                        {
                            if (!MatchesConditions(entryName, entryNameConditions, useAndLogicForEntryName))
                            {
                                skippedByEntryFilter++;
                                continue;
                            }

                            if (useRecursiveChildFilter)
                            {
                                if (!DescendantNameMatches(entryNode, childNameConditions, useAndLogicForChildName))
                                {
                                    skippedByChildFilter++;
                                    continue;
                                }
                            }
                        }

                        searchedEntries++;
                        SearchNodeAndChildren(
                            entryNode,
                            entryNode,
                            searchTerms,
                            searchResultsGrid,
                            dedupeKeys,
                            ref totalHits);
                    }

                    lastStatusText =
                        "Search complete\r\n" +
                        "Hits: " + totalHits + "\r\n" +
                        "Entries searched: " + searchedEntries + "\r\n" +
                        "Skipped by entry filter: " + skippedByEntryFilter + "\r\n" +
                        "Skipped by child filter: " + skippedByChildFilter;

                    statusTextBox.Text = lastStatusText;
                }
                catch (System.Exception ex)
                {
                    string msg = "Search failed\r\n\r\n" + ex.Message;
                    statusTextBox.Text = msg;
                    System.Windows.Forms.MessageBox.Show(
                        msg,
                        "Search Error",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Error);
                }
            };

            clearSearchResultsButton.Click += (sender, args) =>
            {
                searchResultsGrid.Rows.Clear();
            };

            addSelectedPropsButton.Click += (sender, args) =>
            {
                AddCheckedSearchHitsToRules();
            };

            addSelectedEntriesButton.Click += (sender, args) =>
            {
                AddCheckedSearchHitsToEntryFilters();
            };

            var addRowButton = new System.Windows.Forms.Button();
            addRowButton.Text = "Add Rule";
            addRowButton.Left = 12;
            addRowButton.Width = 100;
            addRowButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Bottom;
            addRowButton.Click += (sender, args) =>
            {
                AddRuleRow(true, "", "bool", "set", "", false, "eq", "", true);
            };

            var removeRowButton = new System.Windows.Forms.Button();
            removeRowButton.Text = "Remove Selected";
            removeRowButton.Left = 120;
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
            resetExamplesButton.Text = "Load Starter Rules";
            resetExamplesButton.Left = 260;
            resetExamplesButton.Width = 140;
            resetExamplesButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Bottom;
            resetExamplesButton.Click += (sender, args) =>
            {
                var result = System.Windows.Forms.MessageBox.Show(
                    "Replace the current rules with the generic examples?",
                    "Confirm Reset",
                    System.Windows.Forms.MessageBoxButtons.YesNo,
                    System.Windows.Forms.MessageBoxIcon.Question);

                if (result == System.Windows.Forms.DialogResult.Yes)
                    LoadGenericExampleRows();
            };

            var runButton = new System.Windows.Forms.Button();
            runButton.Text = "Run Batch";
            runButton.Width = 100;
            runButton.Height = 30;
            runButton.Left = 1160;
            runButton.Anchor = System.Windows.Forms.AnchorStyles.Right | System.Windows.Forms.AnchorStyles.Bottom;
            runButton.DialogResult = System.Windows.Forms.DialogResult.None;

            var cancelButton = new System.Windows.Forms.Button();
            cancelButton.Text = "Close";
            cancelButton.Width = 100;
            cancelButton.Height = 30;
            cancelButton.Left = 1272;
            cancelButton.Anchor = System.Windows.Forms.AnchorStyles.Right | System.Windows.Forms.AnchorStyles.Bottom;
            cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;

            var monoBack = System.Drawing.Color.FromArgb(39, 40, 34);
            var monoPanel = System.Drawing.Color.FromArgb(49, 51, 42);
            var monoPanelAlt = System.Drawing.Color.FromArgb(62, 61, 50);
            var monoText = System.Drawing.Color.FromArgb(248, 248, 242);
            var monoMuted = System.Drawing.Color.FromArgb(117, 113, 94);
            var monoAccent = System.Drawing.Color.FromArgb(166, 226, 46);
            var monoOrange = System.Drawing.Color.FromArgb(253, 151, 31);
            var monoBlue = System.Drawing.Color.FromArgb(102, 217, 239);

            void StyleButton(System.Windows.Forms.Button button, bool accent)
            {
                button.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = accent ? monoAccent : monoMuted;
                button.BackColor = accent ? monoAccent : monoPanelAlt;
                button.ForeColor = accent ? monoBack : monoText;
                button.Height = 32;
            }

            void StyleTextBox(System.Windows.Forms.TextBox box)
            {
                box.BackColor = monoPanel;
                box.ForeColor = monoText;
                box.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            }

            void StyleComboBox(System.Windows.Forms.ComboBox box)
            {
                box.BackColor = monoPanel;
                box.ForeColor = monoText;
                box.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            }

            void StyleCheckBox(System.Windows.Forms.CheckBox box)
            {
                box.ForeColor = monoText;
                box.BackColor = monoBack;
            }

            void StyleLabel(System.Windows.Forms.Label label, bool muted)
            {
                label.ForeColor = muted ? monoMuted : monoText;
                label.BackColor = monoBack;
            }

            void StyleGrid(System.Windows.Forms.DataGridView grid, bool readHeavy)
            {
                grid.BackgroundColor = monoPanel;
                grid.GridColor = monoPanelAlt;
                grid.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
                grid.EnableHeadersVisualStyles = false;
                grid.ColumnHeadersDefaultCellStyle.BackColor = monoPanelAlt;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = monoText;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = monoPanelAlt;
                grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = monoText;
                grid.DefaultCellStyle.BackColor = monoPanel;
                grid.DefaultCellStyle.ForeColor = monoText;
                grid.DefaultCellStyle.SelectionBackColor = monoBlue;
                grid.DefaultCellStyle.SelectionForeColor = monoBack;
                grid.RowHeadersDefaultCellStyle.BackColor = monoPanelAlt;
                grid.RowHeadersDefaultCellStyle.ForeColor = monoText;
                grid.AlternatingRowsDefaultCellStyle.BackColor = readHeavy ? monoPanelAlt : monoPanel;
            }

            configForm.BackColor = monoBack;
            configForm.ForeColor = monoText;
            StyleLabel(filtersLabel, false);
            StyleLabel(logicLabel, false);
            StyleLabel(childFiltersLabel, true);
            StyleLabel(childLogicLabel, true);
            StyleLabel(helpLabel, false);
            StyleLabel(searchTermsLabel, false);
            StyleLabel(statusLabel, false);
            StyleTextBox(filtersTextBox);
            StyleTextBox(childFiltersTextBox);
            StyleTextBox(searchTermsTextBox);
            StyleTextBox(statusTextBox);
            StyleComboBox(logicComboBox);
            StyleComboBox(childLogicComboBox);
            StyleCheckBox(selectedOnlyCheckBox);
            StyleCheckBox(recursiveChildFilterCheckBox);
            StyleCheckBox(searchSelectedOnlyCheckBox);
            StyleCheckBox(useSearchHitsAsScopeCheckBox);
            StyleCheckBox(autoAddSearchPropsCheckBox);
            StyleCheckBox(autoAddSearchEntriesCheckBox);
            StyleButton(searchButton, true);
            StyleButton(clearSearchResultsButton, false);
            StyleButton(addSelectedPropsButton, false);
            StyleButton(addSelectedEntriesButton, false);
            StyleButton(addRowButton, false);
            StyleButton(removeRowButton, false);
            StyleButton(resetExamplesButton, false);
            StyleButton(runButton, true);
            StyleButton(cancelButton, false);
            StyleGrid(searchResultsGrid, true);
            StyleGrid(rulesGrid, false);

            configForm.Controls.Add(filtersLabel);
            configForm.Controls.Add(filtersTextBox);
            configForm.Controls.Add(logicLabel);
            configForm.Controls.Add(logicComboBox);
            configForm.Controls.Add(selectedOnlyCheckBox);
            configForm.Controls.Add(recursiveChildFilterCheckBox);
            configForm.Controls.Add(childFiltersLabel);
            configForm.Controls.Add(childFiltersTextBox);
            configForm.Controls.Add(childLogicLabel);
            configForm.Controls.Add(childLogicComboBox);
            configForm.Controls.Add(helpLabel);
            configForm.Controls.Add(searchTermsLabel);
            configForm.Controls.Add(searchTermsTextBox);
            configForm.Controls.Add(searchSelectedOnlyCheckBox);
            configForm.Controls.Add(useSearchHitsAsScopeCheckBox);
            configForm.Controls.Add(autoAddSearchPropsCheckBox);
            configForm.Controls.Add(autoAddSearchEntriesCheckBox);
            configForm.Controls.Add(searchButton);
            configForm.Controls.Add(clearSearchResultsButton);
            configForm.Controls.Add(addSelectedPropsButton);
            configForm.Controls.Add(addSelectedEntriesButton);
            configForm.Controls.Add(searchResultsGrid);
            configForm.Controls.Add(rulesGrid);
            configForm.Controls.Add(statusLabel);
            configForm.Controls.Add(statusTextBox);
            configForm.Controls.Add(addRowButton);
            configForm.Controls.Add(removeRowButton);
            configForm.Controls.Add(resetExamplesButton);
            configForm.Controls.Add(runButton);
            configForm.Controls.Add(cancelButton);

            void UpdateBottomLayout()
            {
                int margin = 12;
                int gap = 18;
                int labelGap = 22;
                int formWidth = configForm.ClientSize.Width;
                int leftPanelWidth = 400;
                int searchButtonsWidth = 290;
                int topRightStart = leftPanelWidth + gap;
                int topRightWidth = formWidth - topRightStart - margin;
                if (topRightWidth < 620)
                    topRightWidth = 620;
                int searchTermsWidth = 260;
                int searchChecksWidth = 330;
                int rightButtonsLeft = formWidth - margin - searchButtonsWidth;
                int searchChecksLeft = rightButtonsLeft - gap - searchChecksWidth;
                int searchTermsLeft = topRightStart;
                int searchTermsAvail = searchChecksLeft - gap - searchTermsLeft;
                if (searchTermsAvail > searchTermsWidth)
                    searchTermsWidth = searchTermsAvail;

                filtersLabel.Left = margin;
                filtersLabel.Top = margin;
                filtersLabel.Width = 260;
                filtersTextBox.Left = margin;
                filtersTextBox.Top = filtersLabel.Bottom + 4;
                filtersTextBox.Width = 260;
                filtersTextBox.Height = 120;

                logicLabel.Left = filtersTextBox.Right + gap;
                logicLabel.Top = margin;
                logicComboBox.Left = logicLabel.Left;
                logicComboBox.Top = filtersTextBox.Top;
                logicComboBox.Width = 120;

                selectedOnlyCheckBox.Left = logicComboBox.Right + gap;
                selectedOnlyCheckBox.Top = filtersTextBox.Top + 2;
                selectedOnlyCheckBox.Width = leftPanelWidth - (selectedOnlyCheckBox.Left - margin);

                helpLabel.Left = topRightStart;
                helpLabel.Top = margin;
                helpLabel.Width = formWidth - topRightStart - margin;
                helpLabel.Height = 68;

                recursiveChildFilterCheckBox.Left = margin;
                recursiveChildFilterCheckBox.Top = filtersTextBox.Bottom + 14;
                recursiveChildFilterCheckBox.Width = 260;
                childFiltersLabel.Left = margin;
                childFiltersLabel.Top = recursiveChildFilterCheckBox.Bottom + 8;
                childFiltersTextBox.Left = margin;
                childFiltersTextBox.Top = childFiltersLabel.Bottom + 4;
                childFiltersTextBox.Width = 260;
                childFiltersTextBox.Height = 120;
                childLogicLabel.Left = margin;
                childLogicLabel.Top = childFiltersTextBox.Bottom + 8;
                childLogicLabel.Width = 140;
                childLogicComboBox.Left = childLogicLabel.Right + 8;
                childLogicComboBox.Top = childFiltersTextBox.Bottom + 4;
                childLogicComboBox.Width = 120;

                searchTermsLabel.Left = searchTermsLeft;
                searchTermsLabel.Top = helpLabel.Bottom + 16;
                searchTermsLabel.Width = searchTermsWidth;
                searchTermsTextBox.Left = searchTermsLeft;
                searchTermsTextBox.Top = searchTermsLabel.Bottom + 4;
                searchTermsTextBox.Width = searchTermsWidth;
                searchTermsTextBox.Height = 120;

                searchSelectedOnlyCheckBox.Left = searchChecksLeft;
                searchSelectedOnlyCheckBox.Top = searchTermsTextBox.Top + 2;
                searchSelectedOnlyCheckBox.Width = searchChecksWidth;
                useSearchHitsAsScopeCheckBox.Left = searchChecksLeft;
                useSearchHitsAsScopeCheckBox.Top = searchSelectedOnlyCheckBox.Bottom + 8;
                useSearchHitsAsScopeCheckBox.Width = searchChecksWidth;
                autoAddSearchPropsCheckBox.Left = searchChecksLeft;
                autoAddSearchPropsCheckBox.Top = useSearchHitsAsScopeCheckBox.Bottom + 8;
                autoAddSearchPropsCheckBox.Width = searchChecksWidth;
                autoAddSearchEntriesCheckBox.Left = searchChecksLeft;
                autoAddSearchEntriesCheckBox.Top = autoAddSearchPropsCheckBox.Bottom + 8;
                autoAddSearchEntriesCheckBox.Width = searchChecksWidth;

                int buttonWidth = (searchButtonsWidth - 12) / 2;
                searchButton.Left = rightButtonsLeft;
                searchButton.Top = searchTermsTextBox.Top;
                searchButton.Width = buttonWidth;
                clearSearchResultsButton.Left = searchButton.Right + 12;
                clearSearchResultsButton.Top = searchButton.Top;
                clearSearchResultsButton.Width = buttonWidth;
                addSelectedPropsButton.Left = rightButtonsLeft;
                addSelectedPropsButton.Top = searchButton.Bottom + 12;
                addSelectedPropsButton.Width = buttonWidth;
                addSelectedEntriesButton.Left = addSelectedPropsButton.Right + 12;
                addSelectedEntriesButton.Top = addSelectedPropsButton.Top;
                addSelectedEntriesButton.Width = buttonWidth;

                int buttonsTop = configForm.ClientSize.Height - 42;
                int statusTop = buttonsTop - 162;
                int searchGridHeight = 155;
                int searchGridGap = 12;
                int ruleSearchGap = 14;
                int searchGridTop = statusTop - searchGridGap - searchGridHeight;
                int ruleTop = System.Math.Max(childLogicComboBox.Bottom, searchTermsTextBox.Bottom) + 18;

                statusLabel.Left = margin;
                statusLabel.Top = statusTop;
                statusTextBox.Left = margin;
                statusTextBox.Top = statusTop + labelGap;
                statusTextBox.Width = formWidth - (margin * 2);
                statusTextBox.Height = buttonsTop - statusTextBox.Top - 14;

                addRowButton.Top = buttonsTop;
                removeRowButton.Top = buttonsTop;
                resetExamplesButton.Top = buttonsTop;
                runButton.Top = buttonsTop;
                cancelButton.Top = buttonsTop;

                rulesGrid.Left = margin;
                rulesGrid.Top = ruleTop;
                rulesGrid.Width = formWidth - (margin * 2);
                rulesGrid.Height = searchGridTop - ruleTop - ruleSearchGap;

                searchResultsGrid.Left = margin;
                searchResultsGrid.Top = searchGridTop;
                searchResultsGrid.Width = formWidth - (margin * 2);
                searchResultsGrid.Height = searchGridHeight;
            }

            configForm.Shown += (sender, args) => UpdateBottomLayout();
            configForm.Resize += (sender, args) => UpdateBottomLayout();

            configForm.CancelButton = cancelButton;
            configForm.KeyPreview = true;

            configForm.KeyDown += (sender, args) =>
            {
                if (args.Control && args.KeyCode == System.Windows.Forms.Keys.Enter)
                {
                    args.SuppressKeyPress = true;
                    runButton.PerformClick();
                }
            };

            var entryNameConditions = new System.Collections.Generic.List<string>();
            bool useAndLogicForEntryName = true;
            bool selectedOnly = false;

            bool useRecursiveChildFilter = false;
            var childNameConditions = new System.Collections.Generic.List<string>();
            bool useAndLogicForChildName = true;

            var parsedRules = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();
            var searchHitScopeEntryNames = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            bool useSearchHitsScope = false;

            runButton.Click += (sender, args) =>
            {
                try
                {
                    rulesGrid.EndEdit();
                    searchResultsGrid.EndEdit();
                    dataGridView.EndEdit();

                    var tempConditions = new System.Collections.Generic.List<string>();
                    foreach (string line in filtersTextBox.Lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            tempConditions.Add(line.Trim());
                    }

                    bool tempUseAndLogic =
                        logicComboBox.SelectedItem == null ||
                        logicComboBox.SelectedItem.ToString().Equals("AND", System.StringComparison.OrdinalIgnoreCase);

                    bool tempSelectedOnly = selectedOnlyCheckBox.Checked;
                    bool tempUseRecursiveChildFilter = recursiveChildFilterCheckBox.Checked;

                    var tempChildConditions = new System.Collections.Generic.List<string>();
                    foreach (string line in childFiltersTextBox.Lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            tempChildConditions.Add(line.Trim());
                    }

                    bool tempUseAndLogicForChild =
                        childLogicComboBox.SelectedItem == null ||
                        childLogicComboBox.SelectedItem.ToString().Equals("AND", System.StringComparison.OrdinalIgnoreCase);

                    if (tempUseRecursiveChildFilter && tempChildConditions.Count == 0)
                    {
                        throw new System.Exception(
                            "Recursive child-name filter is enabled, but no child conditions were entered.");
                    }

                    if (tempSelectedOnly && originallySelectedNode == null)
                    {
                        throw new System.Exception(
                            "Only process currently selected entry is enabled, but there was no selected node when the script started.");
                    }

                    if (autoAddSearchPropsCheckBox.Checked)
                        AddCheckedSearchHitsToRules();

                    if (autoAddSearchEntriesCheckBox.Checked)
                        AddCheckedSearchHitsToEntryFilters();

                    var tempSearchScopeEntryNames = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                    bool tempUseSearchHitsScope = useSearchHitsAsScopeCheckBox.Checked;
                    if (tempUseSearchHitsScope)
                    {
                        foreach (System.Windows.Forms.DataGridViewRow row in searchResultsGrid.Rows)
                        {
                            if (row.IsNewRow) continue;

                            bool useHit = false;
                            object checkedObj = row.Cells["UseHit"].Value;
                            bool parsedChecked;
                            if (TryParseBool(checkedObj, out parsedChecked))
                                useHit = parsedChecked;

                            if (!useHit)
                                continue;

                            string entryName = SafeCellText(row, searchResultsGrid.Columns["ParentEntry"].Index).Trim();
                            if (!string.IsNullOrWhiteSpace(entryName))
                                tempSearchScopeEntryNames.Add(entryName);
                        }

                        if (tempSearchScopeEntryNames.Count == 0)
                            throw new System.Exception("Run-only-on-search-hits is enabled, but no search hits are checked.");
                    }

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
                            throw new System.Exception("An enabled rule has an empty PropName.");

                        if (type != "int" && type != "float" && type != "string" && type != "bool")
                            throw new System.Exception("Rule '" + propName + "' has invalid Type '" + type + "'.");

                        if (!IsValidTargetOperation(targetOperation))
                            throw new System.Exception("Rule '" + propName + "' has invalid TargetOperation '" + targetOperation + "'.");

                        if ((type == "bool" || type == "string") && targetOperation != "set")
                            throw new System.Exception("Rule '" + propName + "' must use TargetOperation 'set' for type '" + type + "'.");

                        if (targetValueText.Length == 0)
                            throw new System.Exception("Rule '" + propName + "' is missing TargetValue.");

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
                                        throw new System.Exception("Rule '" + propName + "' has invalid bool TargetValue '" + targetValueText + "'.");

                                    rule["TargetValue"] = targetBool;

                                    if (useSkip)
                                    {
                                        if (skipValueText.Length == 0)
                                            throw new System.Exception("Rule '" + propName + "' has UseSkip enabled but no SkipValue.");

                                        bool skipBool;
                                        if (!TryParseBool(skipValueText, out skipBool))
                                            throw new System.Exception("Rule '" + propName + "' has invalid bool SkipValue '" + skipValueText + "'.");

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
                                            throw new System.Exception("Rule '" + propName + "' has UseSkip enabled but no SkipValue.");

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
                                        throw new System.Exception("Rule '" + propName + "' has invalid int TargetValue '" + targetValueText + "'.");
                                    }

                                    rule["TargetValue"] = targetInt;

                                    if (useSkip)
                                    {
                                        if (skipValueText.Length == 0)
                                            throw new System.Exception("Rule '" + propName + "' has UseSkip enabled but no SkipValue.");

                                        if (!IsValidNumericSkipOperation(skipOperation))
                                            throw new System.Exception("Rule '" + propName + "' has invalid numeric SkipOperation '" + skipOperation + "'.");

                                        int skipInt;
                                        if (!int.TryParse(
                                            skipValueText,
                                            System.Globalization.NumberStyles.Integer,
                                            System.Globalization.CultureInfo.InvariantCulture,
                                            out skipInt))
                                        {
                                            throw new System.Exception("Rule '" + propName + "' has invalid int SkipValue '" + skipValueText + "'.");
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
                                        throw new System.Exception("Rule '" + propName + "' has invalid float TargetValue '" + targetValueText + "'.");
                                    }

                                    rule["TargetValue"] = targetFloat;

                                    if (useSkip)
                                    {
                                        if (skipValueText.Length == 0)
                                            throw new System.Exception("Rule '" + propName + "' has UseSkip enabled but no SkipValue.");

                                        if (!IsValidNumericSkipOperation(skipOperation))
                                            throw new System.Exception("Rule '" + propName + "' has invalid numeric SkipOperation '" + skipOperation + "'.");

                                        float skipFloat;
                                        if (!float.TryParse(
                                            skipValueText,
                                            System.Globalization.NumberStyles.Float,
                                            System.Globalization.CultureInfo.InvariantCulture,
                                            out skipFloat))
                                        {
                                            throw new System.Exception("Rule '" + propName + "' has invalid float SkipValue '" + skipValueText + "'.");
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
                        throw new System.Exception("No enabled valid rules were found.");

                    SaveCurrentConfig();

                    entryNameConditions = tempConditions;
                    useAndLogicForEntryName = tempUseAndLogic;
                    selectedOnly = tempSelectedOnly;
                    useRecursiveChildFilter = tempUseRecursiveChildFilter;
                    childNameConditions = tempChildConditions;
                    useAndLogicForChildName = tempUseAndLogicForChild;
                    parsedRules = tempRules;
                    searchHitScopeEntryNames = tempSearchScopeEntryNames;
                    useSearchHitsScope = tempUseSearchHitsScope;

                    configForm.Tag = "RUN";
                    configForm.Close();
                }
                catch (System.Exception ex)
                {
                    string detailedError =
                        "Validation error\r\n\r\n" +
                        ex.Message;

                    statusTextBox.Text = detailedError;

                    System.Windows.Forms.MessageBox.Show(
                        detailedError,
                        "Validation Error",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Error);
                }
            };

            var dialogResult = configForm.ShowDialog(form);
            if (!object.Equals(configForm.Tag, "RUN"))
                break;

            try
            {
                var propertyRuleMap =
                    new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, object>>(
                        System.StringComparer.OrdinalIgnoreCase);

                foreach (var rule in parsedRules)
                {
                    string propName = rule["PropName"].ToString();
                    propertyRuleMap[propName] = rule;
                }

                var entryNodes = new System.Collections.Generic.List<System.Windows.Forms.TreeNode>();

                if (selectedOnly)
                {
                    if (originallySelectedNode == null)
                    {
                        throw new System.Exception(
                            "Selected-only mode was enabled, but no original selected node was available.");
                    }

                    entryNodes.Add(originallySelectedNode);
                }
                else
                {
                    entryNodes = GetProcessableEntryNodes(tree);

                    if (entryNodes.Count == 0)
                    {
                        throw new System.Exception(
                            "No processable entry nodes were found under Export Data.\r\n" +
                            "The asset may use a different tree layout than expected.");
                    }
                }

                int matchedEntries = 0;
                int editedEntries = 0;
                int editedValues = 0;
                int editedIsZeroFlags = 0;
                int skippedEntries = 0;
                int skippedByChildFilter = 0;
                int skippedBySearchScope = 0;
                int rowsMatchedByRule = 0;

                var targetPropertyNames =
                    new System.Collections.Generic.HashSet<string>(
                        propertyRuleMap.Keys,
                        System.StringComparer.OrdinalIgnoreCase);

                foreach (System.Windows.Forms.TreeNode entryNode in entryNodes)
                {
                    string entryName = entryNode.Text ?? string.Empty;

                    if (useSearchHitsScope && !searchHitScopeEntryNames.Contains(entryName))
                    {
                        skippedEntries++;
                        skippedBySearchScope++;
                        continue;
                    }

                    if (!selectedOnly)
                    {
                        if (!MatchesConditions(entryName, entryNameConditions, useAndLogicForEntryName))
                        {
                            skippedEntries++;
                            continue;
                        }

                        if (useRecursiveChildFilter)
                        {
                            if (!DescendantNameMatches(entryNode, childNameConditions, useAndLogicForChildName))
                            {
                                skippedEntries++;
                                skippedByChildFilter++;
                                continue;
                            }
                        }
                    }

                    matchedEntries++;

                    var editableNode = ResolveBestEditableNode(
                        entryNode,
                        dataGridView,
                        nameColumnIndex,
                        targetPropertyNames);

                    SelectNode(editableNode);

                    bool changedThisEntry = false;

                    foreach (System.Windows.Forms.DataGridViewRow row in dataGridView.Rows)
                    {
                        if (row.IsNewRow) continue;
                        if (row.Cells[nameColumnIndex] == null || row.Cells[valueColumnIndex] == null) continue;

                        object propNameObject = row.Cells[nameColumnIndex].Value;
                        if (propNameObject == null) continue;

                        string propName = propNameObject.ToString().Trim();
                        if (!propertyRuleMap.ContainsKey(propName)) continue;

                        rowsMatchedByRule++;

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

                                    if (System.Math.Abs(newValue - currentValue) >= FloatEpsilon)
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

                lastStatusText =
                    "Done\r\n" +
                    "Matched entries: " + matchedEntries + "\r\n" +
                    "Rows matched by rule: " + rowsMatchedByRule + "\r\n" +
                    "Edited entries: " + editedEntries + "\r\n" +
                    "Edited values: " + editedValues + "\r\n" +
                    "Edited Is Zero: " + editedIsZeroFlags + "\r\n" +
                    "Skipped: " + skippedEntries + "\r\n" +
                    "Skipped by child filter: " + skippedByChildFilter + "\r\n" +
                    "Skipped by search scope: " + skippedBySearchScope;

                System.Windows.Forms.MessageBox.Show(
                    "Done.\r\n\r\n" +
                    "Edited values: " + editedValues + "\r\n" +
                    "Edited entries: " + editedEntries,
                    "Batch Edit Complete",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Information);

                runAgain = true;
            }
            catch (System.Exception ex)
            {
                lastStatusText =
                    "Run failed\r\n\r\n" +
                    ex.Message + "\r\n\r\n" +
                    ex.ToString();

                System.Windows.Forms.MessageBox.Show(
                    lastStatusText,
                    "Script Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);

                runAgain = true;
            }
        }
    }
    catch (System.Exception ex)
    {
        System.Windows.Forms.MessageBox.Show(
            "Unhandled script error\r\n\r\n" + ex.ToString(),
            "Script Error",
            System.Windows.Forms.MessageBoxButtons.OK,
            System.Windows.Forms.MessageBoxIcon.Error);
    }
});
