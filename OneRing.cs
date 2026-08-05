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

        // Set by the Close button (or Escape) while a search is running, to cooperatively
        // stop it: checked at the traversal's SelectNode-driven DoEvents checkpoints below,
        // rather than tearing the form down out from under a loop that's still iterating its
        // controls/collections. Declared out here (not inside the per-dialog loop further
        // down) since SearchNodeAndChildren, an outer-scope local function, needs to read it.
        bool cancelRequested = false;

        string configDirectory = System.Windows.Forms.Application.LocalUserAppDataPath;
        string configFilePath = System.IO.Path.Combine(configDirectory, "UAssetGUI_BatchRuleEditor_LastConfig.txt");

        int nameColumnIndex = -1;
        int valueColumnIndex = -1;
        int isZeroColumnIndex = -1;

        // Every column other than Name/Property Name and Is Zero is treated as a "value"
        // column. Some property/struct layouts expose more than one (e.g. Value plus
        // Value2/Value3/Value4 for multi-component values), and the exact set is whatever
        // the live grid happens to show for whichever node ends up selected below -
        // discovered here (rather than hardcoded) so it tracks the real layout, and
        // computed up front since local functions below need it declared first.
        var valueColumnHeaders = new System.Collections.Generic.List<string>();

        // Local functions are usable before their textual declaration within the same
        // block, so this can freely call ones defined further down (SelectNode,
        // GetProcessableEntryNodes, EnumerateNodeAndDescendants).
        void ScanCurrentGridColumns()
        {
            nameColumnIndex = -1;
            valueColumnIndex = -1;
            isZeroColumnIndex = -1;
            valueColumnHeaders.Clear();

            for (int columnIndex = 0; columnIndex < dataGridView.Columns.Count; columnIndex++)
            {
                string headerText = dataGridView.Columns[columnIndex].HeaderText ?? string.Empty;
                if (headerText == "Name" || headerText == "Property Name") nameColumnIndex = columnIndex;
                if (headerText == "Value") valueColumnIndex = columnIndex;
                if (headerText == "Is Zero") isZeroColumnIndex = columnIndex;

                if (headerText.Length > 0 &&
                    headerText != "Name" &&
                    headerText != "Property Name" &&
                    headerText != "Is Zero" &&
                    !valueColumnHeaders.Contains(headerText))
                {
                    valueColumnHeaders.Add(headerText);
                }
            }
        }

        ScanCurrentGridColumns();

        bool CurrentGridHasPopulatedNameAndValueColumns()
        {
            bool hasName = false;
            bool hasValue = false;
            for (int columnIndex = 0; columnIndex < dataGridView.Columns.Count; columnIndex++)
            {
                string headerText = dataGridView.Columns[columnIndex].HeaderText ?? string.Empty;
                if (headerText == "Name" || headerText == "Property Name") hasName = true;
                if (headerText == "Value") hasValue = true;
            }

            return hasName && hasValue && dataGridView.Rows.Count > 0;
        }

        if (nameColumnIndex < 0 || valueColumnIndex < 0)
        {
            // Whatever was selected when the script started (nothing, or a container node
            // like "Export Data" itself) isn't showing a property grid. Don't require the
            // user to have pre-selected the right node - look for the data ourselves.
            var discoveryEntries = GetProcessableEntryNodes(tree);

            System.Windows.Forms.TreeNode discoveredNode = null;
            int nodesScanned = 0;
            const int maxDiscoveryNodesToScan = 300;

            // Most entries show their own property grid directly when selected, so try each
            // entry root first - cheap, and avoids materializing any entry's full descendant
            // subtree (EnumerateNodeAndDescendants is unbounded) unless actually needed.
            foreach (var discoveryEntry in discoveryEntries)
            {
                SelectNode(discoveryEntry);
                nodesScanned++;

                if (CurrentGridHasPopulatedNameAndValueColumns())
                {
                    discoveredNode = discoveryEntry;
                    break;
                }

                if (nodesScanned >= maxDiscoveryNodesToScan)
                    break;
            }

            // Fallback for layouts where entries are pure containers (e.g. nested-struct-only
            // rows): walk descendants, but only under the first entry and capped, to bound
            // worst-case cost regardless of asset size.
            if (discoveredNode == null && discoveryEntries.Count > 0)
            {
                foreach (var candidate in EnumerateNodeAndDescendants(discoveryEntries[0]))
                {
                    SelectNode(candidate);
                    nodesScanned++;

                    if (CurrentGridHasPopulatedNameAndValueColumns())
                    {
                        discoveredNode = candidate;
                        break;
                    }

                    if (nodesScanned >= maxDiscoveryNodesToScan)
                        break;
                }
            }

            if (discoveredNode != null)
                ScanCurrentGridColumns();

            // Restore whatever was actually selected before the script ran (if anything) so
            // the discovery walk above doesn't leave an arbitrary node highlighted.
            if (originallySelectedNode != null)
                SelectNode(originallySelectedNode);
        }

        if (nameColumnIndex < 0 || valueColumnIndex < 0)
        {
            System.Windows.Forms.MessageBox.Show(
                "Could not find required grid columns anywhere under Export Data.\r\n\r\n" +
                "Name column found: " + (nameColumnIndex >= 0 ? "Yes" : "No") + "\r\n" +
                "Value column found: " + (valueColumnIndex >= 0 ? "Yes" : "No") + "\r\n\r\n" +
                "The asset may use a different tree layout than expected, or have no editable properties.",
                "Script Error",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);
            return;
        }

        string defaultValueColumnHeader = valueColumnHeaders.Contains("Value")
            ? "Value"
            : (valueColumnHeaders.Count > 0 ? valueColumnHeaders[0] : "Value");

        // NOTE: the four traversal helpers below are iterative (explicit-stack) rather than
        // recursive. UE assets can nest structs/arrays deeply enough that naive recursion risks
        // an uncatchable StackOverflowException, which would kill the host process outright.

        System.Windows.Forms.TreeNode FindFirstNodeRecursive(
            System.Windows.Forms.TreeNodeCollection nodes,
            System.Func<System.Windows.Forms.TreeNode, bool> predicate)
        {
            var stack = new System.Collections.Generic.Stack<System.Windows.Forms.TreeNode>();
            for (int i = nodes.Count - 1; i >= 0; i--)
                stack.Push(nodes[i]);

            while (stack.Count > 0)
            {
                var node = stack.Pop();

                if (predicate(node))
                    return node;

                for (int i = node.Nodes.Count - 1; i >= 0; i--)
                    stack.Push(node.Nodes[i]);
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

            var stack = new System.Collections.Generic.Stack<System.Windows.Forms.TreeNode>();
            for (int i = root.Nodes.Count - 1; i >= 0; i--)
                stack.Push(root.Nodes[i]);

            while (stack.Count > 0)
            {
                var node = stack.Pop();

                if (predicate(node))
                    results.Add(node);

                for (int i = node.Nodes.Count - 1; i >= 0; i--)
                    stack.Push(node.Nodes[i]);
            }
        }

        System.Collections.Generic.List<System.Windows.Forms.TreeNode> EnumerateNodeAndDescendants(
            System.Windows.Forms.TreeNode root)
        {
            var results = new System.Collections.Generic.List<System.Windows.Forms.TreeNode>();
            if (root == null)
                return results;

            var stack = new System.Collections.Generic.Stack<System.Windows.Forms.TreeNode>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                results.Add(node);

                for (int i = node.Nodes.Count - 1; i >= 0; i--)
                    stack.Push(node.Nodes[i]);
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

            // Plain substring containment, not a \b-bounded whole-word regex match: UE
            // property/entry names are PascalCase or underscore_joined compounds (e.g.
            // "MaxCoolTime", "CoolTime_Base") with no real word-boundary between an
            // adjacent letter/underscore, so a word-boundary match effectively demanded
            // the token equal the entire identifier - silently zeroing out ordinary
            // partial-name searches.
            return source.IndexOf(token.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0;
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

            var stack = new System.Collections.Generic.Stack<System.Windows.Forms.TreeNode>();
            foreach (System.Windows.Forms.TreeNode child in root.Nodes)
                stack.Push(child);

            while (stack.Count > 0)
            {
                var node = stack.Pop();

                if (MatchesConditions(node.Text ?? string.Empty, conditions, useAndLogic))
                    return true;

                foreach (System.Windows.Forms.TreeNode child in node.Nodes)
                    stack.Push(child);
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

        int ApplyIntOperation(int currentValue, string operation, int targetValue, out bool divByZeroSkipped)
        {
            divByZeroSkipped = false;
            switch ((operation ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "set": return targetValue;
                case "add": return currentValue + targetValue;
                case "sub": return currentValue - targetValue;
                case "mul": return currentValue * targetValue;
                case "div":
                    if (targetValue == 0) { divByZeroSkipped = true; return currentValue; }
                    return currentValue / targetValue;
                default: return currentValue;
            }
        }

        float ApplyFloatOperation(float currentValue, string operation, float targetValue, out bool divByZeroSkipped)
        {
            divByZeroSkipped = false;
            switch ((operation ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "set": return targetValue;
                case "add": return currentValue + targetValue;
                case "sub": return currentValue - targetValue;
                case "mul": return currentValue * targetValue;
                case "div":
                    if (System.Math.Abs(targetValue) < FloatEpsilon) { divByZeroSkipped = true; return currentValue; }
                    return currentValue / targetValue;
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

            // node.FullPath is Text-only and collides for same-named siblings (e.g. two
            // export entries with an identical display name), which would silently drop
            // all but one of them here. BuildManualNodePath disambiguates same-named
            // siblings with a "#index" suffix, so it's safe to dedupe with instead.
            var deduped = new System.Collections.Generic.List<System.Windows.Forms.TreeNode>();
            var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);

            foreach (var node in results)
            {
                string key = BuildManualNodePath(node);
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

        // Segment label disambiguates same-named siblings (e.g. repeated struct/array element
        // names) by suffixing the sibling index, so manual paths uniquely identify one node
        // even when Text alone is ambiguous (fixes double-click-jumps-to-wrong-node).
        string BuildPathSegmentLabel(System.Windows.Forms.TreeNode node)
        {
            string text = node.Text ?? string.Empty;

            var siblings = node.Parent == null ? tree.Nodes : node.Parent.Nodes;
            int sameNameCount = 0;
            foreach (System.Windows.Forms.TreeNode sibling in siblings)
            {
                if (string.Equals(sibling.Text ?? string.Empty, text, System.StringComparison.Ordinal))
                    sameNameCount++;
            }

            return sameNameCount > 1 ? text + "#" + node.Index : text;
        }

        string BuildManualNodePath(System.Windows.Forms.TreeNode node)
        {
            if (node == null)
                return string.Empty;

            var parts = new System.Collections.Generic.List<string>();
            var current = node;

            while (current != null)
            {
                parts.Insert(0, BuildPathSegmentLabel(current));
                current = current.Parent;
            }

            return string.Join(" -> ", parts.ToArray());
        }

        System.Windows.Forms.TreeNode FindNodeByManualPath(System.Windows.Forms.TreeView sourceTree, string manualPath)
        {
            if (sourceTree == null || string.IsNullOrWhiteSpace(manualPath))
                return null;

            var stack = new System.Collections.Generic.Stack<System.Windows.Forms.TreeNode>();
            foreach (System.Windows.Forms.TreeNode root in sourceTree.Nodes)
                stack.Push(root);

            while (stack.Count > 0)
            {
                var node = stack.Pop();

                if (string.Equals(BuildManualNodePath(node), manualPath, System.StringComparison.Ordinal))
                    return node;

                foreach (System.Windows.Forms.TreeNode child in node.Nodes)
                    stack.Push(child);
            }

            return null;
        }

        bool GridContainsAnyTargetProperty(
            System.Windows.Forms.DataGridView grid,
            int nameColumnIndexParam,
            System.Collections.Generic.ICollection<string> propertyNames)
        {
            if (grid == null || nameColumnIndexParam < 0 || propertyNames == null || propertyNames.Count == 0)
                return false;

            foreach (System.Windows.Forms.DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Cells[nameColumnIndexParam] == null) continue;

                object nameObj = row.Cells[nameColumnIndexParam].Value;
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
            int nameColumnIndexParam,
            System.Collections.Generic.ICollection<string> propertyNames)
        {
            var candidates = EnumerateNodeAndDescendants(entryNode);

            foreach (var candidate in candidates)
            {
                SelectNode(candidate);

                if (GridContainsAnyTargetProperty(grid, nameColumnIndexParam, propertyNames))
                    return candidate;
            }

            return entryNode;
        }

        // Applies all matching rules to whichever grid is currently displayed for one entry.
        // Column indices/maps are passed in per-call (resolved fresh per entry by the caller)
        // rather than captured once globally, since different entries/export types can present
        // different grid layouts. Returns true if any cell on this entry was actually changed.
        bool ProcessEntryRows(
            System.Windows.Forms.DataGridView grid,
            int nameColumnIndexForEntry,
            System.Collections.Generic.Dictionary<string, int> entryColumnMap,
            int isZeroColumnIndexForEntry,
            System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, object>> propertyRuleMap,
            ref int rowsMatchedByRule,
            ref int editedValues,
            ref int editedIsZeroFlags,
            ref int skippedDivByZero,
            ref int skippedRowsMissingValueColumn)
        {
            bool changedThisEntry = false;

            foreach (System.Windows.Forms.DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                if (nameColumnIndexForEntry >= row.Cells.Count) continue;
                if (row.Cells[nameColumnIndexForEntry] == null) continue;

                object propNameObject = row.Cells[nameColumnIndexForEntry].Value;
                if (propNameObject == null) continue;

                string propName = propNameObject.ToString().Trim();
                if (!propertyRuleMap.ContainsKey(propName)) continue;

                rowsMatchedByRule++;

                var rule = propertyRuleMap[propName];

                // Each rule targets whichever value column the user picked for it (Value,
                // Value2, ...), resolved against this entry's own live column layout.
                string valueColumnHeader = rule.ContainsKey("ValueColumnHeader") && rule["ValueColumnHeader"] != null
                    ? rule["ValueColumnHeader"].ToString()
                    : "Value";

                int valueColumnIndexForEntry = entryColumnMap.ContainsKey(valueColumnHeader)
                    ? entryColumnMap[valueColumnHeader]
                    : -1;

                if (valueColumnIndexForEntry < 0 ||
                    valueColumnIndexForEntry >= row.Cells.Count ||
                    row.Cells[valueColumnIndexForEntry] == null)
                {
                    skippedRowsMissingValueColumn++;
                    continue;
                }

                string type = rule["Type"].ToString().Trim().ToLowerInvariant();
                object currentValueObject = row.Cells[valueColumnIndexForEntry].Value;

                // "Is Zero" correction always runs for null-like values - no longer a per-rule
                // toggle.
                if (isZeroColumnIndexForEntry >= 0 &&
                    isZeroColumnIndexForEntry < row.Cells.Count &&
                    row.Cells[isZeroColumnIndexForEntry] != null)
                {
                    if (IsNullLikeByType(type, currentValueObject))
                    {
                        object currentIsZeroObject = row.Cells[isZeroColumnIndexForEntry].Value;
                        bool currentIsZero = false;
                        bool parsedCurrentIsZero;
                        if (TryParseBool(currentIsZeroObject, out parsedCurrentIsZero))
                            currentIsZero = parsedCurrentIsZero;

                        if (!currentIsZero)
                        {
                            row.Cells[isZeroColumnIndexForEntry].Value = "True";
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
                                row.Cells[valueColumnIndexForEntry].Value = targetValue ? "True" : "False";
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
                                row.Cells[valueColumnIndexForEntry].Value = targetValue;
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

                            bool divByZeroSkipped;
                            int newValue = ApplyIntOperation(currentValue, targetOperation, targetValue, out divByZeroSkipped);

                            if (divByZeroSkipped)
                            {
                                skippedDivByZero++;
                                break;
                            }

                            if (newValue != currentValue)
                            {
                                row.Cells[valueColumnIndexForEntry].Value = newValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
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

                            bool divByZeroSkipped;
                            float newValue = ApplyFloatOperation(currentValue, targetOperation, targetValue, out divByZeroSkipped);

                            if (divByZeroSkipped)
                            {
                                skippedDivByZero++;
                                break;
                            }

                            if (System.Math.Abs(newValue - currentValue) >= FloatEpsilon)
                            {
                                row.Cells[valueColumnIndexForEntry].Value = FormatFloat(newValue);
                                editedValues++;
                                changedThisEntry = true;
                            }

                            break;
                        }
                }
            }

            return changedThisEntry;
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

                var rowValues = new System.Collections.Generic.List<object>
                {
                    false,
                    searchTerm,
                    entryNode.Text,
                    currentNode.Text,
                    relativePath,
                    matchedSource,
                    propName,
                    matchedText,
                };

                // Alongside whichever column the search actually matched on, also surface every
                // value column on this same property row so it's reviewable/editable in one place.
                foreach (string valueHeader in valueColumnHeaders)
                {
                    int valueHeaderColIndex = columnMap.ContainsKey(valueHeader) ? columnMap[valueHeader] : -1;
                    rowValues.Add(valueHeaderColIndex >= 0 ? SafeCellText(row, valueHeaderColIndex) : string.Empty);
                }

                rowValues.Add(manualPath);
                rowValues.Add(BuildManualNodePath(entryNode));

                resultsGrid.Rows.Add(rowValues.ToArray());

                totalHits++;
            }
        }

        void SearchNodeAndChildren(
            System.Windows.Forms.TreeNode rootNode,
            System.Windows.Forms.TreeNode entryNode,
            System.Collections.Generic.List<string> searchTerms,
            System.Windows.Forms.DataGridView resultsGrid,
            System.Collections.Generic.HashSet<string> dedupeKeys,
            ref int totalHits)
        {
            if (rootNode == null)
                return;

            // Iterative (explicit-stack) traversal: avoids uncatchable stack overflow on
            // deeply nested assets, since this also drives UI selection per node.
            var stack = new System.Collections.Generic.Stack<System.Windows.Forms.TreeNode>();
            stack.Push(rootNode);

            while (stack.Count > 0)
            {
                var currentNode = stack.Pop();

                SelectNode(currentNode);

                // SelectNode() is the only place this loop pumps DoEvents, so it's the only
                // point a Close-while-busy click can actually get processed and flip this -
                // check right after it to stop promptly instead of continuing to grind through
                // the rest of the tree.
                if (cancelRequested)
                    return;

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
                            // Tree-node-text hits aren't a property row, so there's nothing to
                            // put in the value columns - leave them blank (not live-editable).
                            var rowValues = new System.Collections.Generic.List<object>
                            {
                                false,
                                searchTerm,
                                entryNode.Text,
                                currentNode.Text,
                                relativePath,
                                "Tree Node",
                                "",
                                currentNode.Text,
                            };

                            for (int valueColumnCount = 0; valueColumnCount < valueColumnHeaders.Count; valueColumnCount++)
                                rowValues.Add(string.Empty);

                            rowValues.Add(manualPath);
                            rowValues.Add(BuildManualNodePath(entryNode));

                            resultsGrid.Rows.Add(rowValues.ToArray());

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

                for (int i = currentNode.Nodes.Count - 1; i >= 0; i--)
                    stack.Push(currentNode.Nodes[i]);
            }
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

            // Positioned in the bottom button row alongside the rule-management and
            // primary-action buttons - see UpdateBottomLayout.
            var searchButton = new System.Windows.Forms.Button();
            searchButton.Text = "Scan Targets";
            searchButton.Width = 140;
            searchButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Bottom;

            var clearSearchResultsButton = new System.Windows.Forms.Button();
            clearSearchResultsButton.Text = "Clear Results";
            clearSearchResultsButton.Width = 110;
            clearSearchResultsButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Bottom;

            var addSelectedPropsButton = new System.Windows.Forms.Button();
            addSelectedPropsButton.Text = "Promote Props to Rules";
            addSelectedPropsButton.Width = 150;
            addSelectedPropsButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Bottom;

            var addSelectedEntriesButton = new System.Windows.Forms.Button();
            addSelectedEntriesButton.Text = "Promote Entries to Filters";
            addSelectedEntriesButton.Width = 160;
            addSelectedEntriesButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Bottom;

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

            // Hidden plumbing column: the sibling-index-disambiguated path of the hit's parent
            // entry (not the matched node itself). Lets "run only on search hits" restrict a
            // batch run to the exact entry instance that was checked, even when another entry
            // elsewhere in the tree shares the same display name.
            var entryManualPathColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            entryManualPathColumn.Name = "EntryManualPath";
            entryManualPathColumn.HeaderText = "EntryManualPath";
            entryManualPathColumn.ReadOnly = true;
            entryManualPathColumn.Visible = false;

            searchResultsGrid.Columns.Add(searchSelectColumn);
            searchResultsGrid.Columns.Add(termColumn);
            searchResultsGrid.Columns.Add(entryColumn);
            searchResultsGrid.Columns.Add(nodeColumn);
            searchResultsGrid.Columns.Add(relativePathColumn);
            searchResultsGrid.Columns.Add(sourceColumn);
            searchResultsGrid.Columns.Add(propColumn);
            searchResultsGrid.Columns.Add(textColumn);

            // One editable column per live value column (Value, Value2, ...). Edited cells are
            // written straight back to the live property grid - see the searchResultsGrid
            // CellEndEdit handler wired up further below, alongside the other result-grid
            // event handlers.
            var valueDisplayColumnNames = new System.Collections.Generic.List<string>();
            foreach (string header in valueColumnHeaders)
            {
                var valueDisplayColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
                valueDisplayColumn.Name = "Val_" + header;
                valueDisplayColumn.HeaderText = header;
                valueDisplayColumn.Width = 120;
                valueDisplayColumn.ReadOnly = false;
                searchResultsGrid.Columns.Add(valueDisplayColumn);
                valueDisplayColumnNames.Add(valueDisplayColumn.Name);
            }

            searchResultsGrid.Columns.Add(manualPathColumn);
            searchResultsGrid.Columns.Add(entryManualPathColumn);

            var editableValueColumnIndexes = new System.Collections.Generic.HashSet<int>();
            foreach (string columnName in valueDisplayColumnNames)
                editableValueColumnIndexes.Add(searchResultsGrid.Columns[columnName].Index);

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

            // Which live grid column a rule writes its value into. Populated from whatever
            // value-like columns the live property grid actually shows (Value, Value2, ...) -
            // see valueColumnHeaders near the top of the script.
            var valueColumnColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            valueColumnColumn.Name = "ValueColumn";
            valueColumnColumn.HeaderText = "ValueColumn";
            valueColumnColumn.Width = 100;
            valueColumnColumn.DisplayStyle = System.Windows.Forms.DataGridViewComboBoxDisplayStyle.DropDownButton;
            valueColumnColumn.Items.AddRange(valueColumnHeaders.ToArray());

            rulesGrid.Columns.Add(enabledColumn);
            rulesGrid.Columns.Add(propNameColumn);
            rulesGrid.Columns.Add(typeColumn);
            rulesGrid.Columns.Add(targetOperationColumn);
            rulesGrid.Columns.Add(targetValueColumn);
            rulesGrid.Columns.Add(valueColumnColumn);
            rulesGrid.Columns.Add(useSkipColumn);
            rulesGrid.Columns.Add(skipOperationColumn);
            rulesGrid.Columns.Add(skipValueColumn);

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
                string valueColumnHeader,
                bool useSkip,
                string skipOperation,
                string skipValue)
            {
                rulesGrid.Rows.Add(
                    enabled,
                    propName,
                    type,
                    targetOperation,
                    targetValue,
                    string.IsNullOrWhiteSpace(valueColumnHeader) ? defaultValueColumnHeader : valueColumnHeader,
                    useSkip,
                    skipOperation,
                    skipValue);
            }

            void LoadGenericExampleRows()
            {
                rulesGrid.Rows.Clear();
                AddRuleRow(true, "ExampleFloatProp", "float", "mul", "0.9", defaultValueColumnHeader, true, "eq", "0");
                AddRuleRow(true, "ExampleIntProp", "int", "add", "5", defaultValueColumnHeader, true, "lt", "0");
                AddRuleRow(true, "ExampleBoolProp", "bool", "set", "true", defaultValueColumnHeader, true, "eq", "true");
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

                                bool parsedBool;
                                if (TryParseBool(UnescapeConfig(parts[1]), out parsedBool)) enabled = parsedBool;
                                if (TryParseBool(UnescapeConfig(parts[6]), out parsedBool)) useSkip = parsedBool;

                                // Field 9 used to be the "Set Is Zero When Null-Like" toggle
                                // (that correction now always runs, so the toggle is gone); it's
                                // now the chosen value column. Configs saved before this change
                                // have "True"/"False" there instead of a real header, so fall
                                // back to the default column in that case.
                                string valueColumnHeader = UnescapeConfig(parts[9]);
                                if (!valueColumnHeaders.Contains(valueColumnHeader))
                                    valueColumnHeader = defaultValueColumnHeader;

                                AddRuleRow(
                                    enabled,
                                    UnescapeConfig(parts[2]),
                                    UnescapeConfig(parts[3]),
                                    UnescapeConfig(parts[4]),
                                    UnescapeConfig(parts[5]),
                                    valueColumnHeader,
                                    useSkip,
                                    UnescapeConfig(parts[7]),
                                    UnescapeConfig(parts[8]));
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
                    string valueColumnHeader = row.Cells["ValueColumn"].Value == null ? defaultValueColumnHeader : row.Cells["ValueColumn"].Value.ToString();

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
                        EscapeConfig(valueColumnHeader));
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

            // Guesses a rule type from a sample value string rather than always assuming bool,
            // since promoting a search hit for an int/float/string property as a "bool set true"
            // rule silently no-ops at run time (TryParseBool fails on the real value, row is
            // skipped with no diagnostic). "true"/"false" literals only -> bool, so numeric
            // "0"/"1" values aren't misclassified as booleans.
            string InferTypeFromSampleText(string sampleText)
            {
                if (string.IsNullOrWhiteSpace(sampleText))
                    return "bool";

                string trimmed = sampleText.Trim();

                if (trimmed.Equals("true", System.StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("false", System.StringComparison.OrdinalIgnoreCase))
                    return "bool";

                int intSample;
                if (int.TryParse(trimmed, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out intSample))
                    return "int";

                float floatSample;
                if (float.TryParse(trimmed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out floatSample))
                    return "float";

                return "string";
            }

            void AddUniqueRuleForProperty(string propName, string sampleValueText)
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

                string inferredType = InferTypeFromSampleText(sampleValueText);
                string defaultTargetValue = inferredType == "bool" ? "true" : string.Empty;

                AddRuleRow(true, propName.Trim(), inferredType, "set", defaultTargetValue, defaultValueColumnHeader, false, "eq", "");
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
                    string matchedSource = SafeCellText(row, searchResultsGrid.Columns["MatchedSource"].Index).Trim();
                    string matchedText = SafeCellText(row, searchResultsGrid.Columns["MatchedText"].Index).Trim();

                    // Only trust MatchedText as a value sample when the hit actually matched on
                    // the property's Value column; a Name/tree-node match's MatchedText is the
                    // name itself, not a representative value.
                    string sampleValueText = matchedSource.Equals("Property Value", System.StringComparison.OrdinalIgnoreCase)
                        ? matchedText
                        : null;

                    if (!string.IsNullOrWhiteSpace(propName))
                        AddUniqueRuleForProperty(propName, sampleValueText);
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

            var addRowButton = new System.Windows.Forms.Button();
            addRowButton.Text = "Add Rule";
            addRowButton.Left = 12;
            addRowButton.Width = 100;
            addRowButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Bottom;
            addRowButton.Click += (sender, args) =>
            {
                AddRuleRow(true, "", "bool", "set", "", defaultValueColumnHeader, false, "eq", "");
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

            // No DialogResult here - closing is handled explicitly by cancelButton.Click
            // below (wired once isBusy/cancelRequested are in scope), so a click while a
            // search is running interrupts it instead of auto-closing the form underneath it.
            var cancelButton = new System.Windows.Forms.Button();
            cancelButton.Text = "Close";
            cancelButton.Width = 100;
            cancelButton.Height = 30;
            cancelButton.Left = 1272;
            cancelButton.Anchor = System.Windows.Forms.AnchorStyles.Right | System.Windows.Forms.AnchorStyles.Bottom;

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

            // SelectNode() pumps Application.DoEvents() while scanning/editing, so the UI
            // remains responsive to clicks during a long batch. Without a busy guard, a second
            // click on Run/Search mid-operation can dispose or re-enter the very
            // controls/collections the loop is iterating, corrupting state or throwing
            // ObjectDisposedException. isBusy disables the interactive controls for the
            // duration - except Close, which stays clickable so it can interrupt the search
            // (see cancelButton.Click and cancelRequested) instead of just being blocked.
            bool isBusy = false;

            void SetBusyState(bool busy)
            {
                isBusy = busy;
                searchButton.Enabled = !busy;
                clearSearchResultsButton.Enabled = !busy;
                addSelectedPropsButton.Enabled = !busy;
                addSelectedEntriesButton.Enabled = !busy;
                addRowButton.Enabled = !busy;
                removeRowButton.Enabled = !busy;
                resetExamplesButton.Enabled = !busy;
                runButton.Enabled = !busy;
                cancelButton.Text = busy ? "Cancel" : "Close";
            }

            // While a search is running this interrupts it (see cancelRequested,
            // checked inside SearchNodeAndChildren) rather than closing outright - closing
            // happens once the interrupted search has actually unwound, back in
            // searchButton.Click. When idle, it just closes normally.
            cancelButton.Click += (sender, args) =>
            {
                if (isBusy)
                {
                    cancelRequested = true;
                    return;
                }

                configForm.Close();
            };

            searchButton.Click += (sender, args) =>
            {
                if (isBusy)
                    return;

                SetBusyState(true);
                cancelRequested = false;
                try
                {
                    rulesGrid.EndEdit();
                    searchResultsGrid.EndEdit();
                    SaveCurrentConfig();

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
                        if (cancelRequested)
                            break;

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
                        (cancelRequested ? "Search interrupted (Close was pressed)\r\n" : "Search complete\r\n") +
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
                finally
                {
                    SetBusyState(false);
                }

                // The interrupt only stopped the search loop itself - now that it's actually
                // safe (isBusy is clear, nothing is still iterating live controls), honor the
                // close the user originally asked for.
                if (cancelRequested)
                    configForm.Close();
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

            // Editing one of the dynamic value columns writes straight back to the live
            // property grid: re-select the hit's node (repopulates dataGridView), find the
            // same-named property row on it, and push the edited text into that column.
            searchResultsGrid.CellEndEdit += (sender, args) =>
            {
                if (args.RowIndex < 0 || !editableValueColumnIndexes.Contains(args.ColumnIndex))
                    return;

                var row = searchResultsGrid.Rows[args.RowIndex];

                string matchedSource = SafeCellText(row, searchResultsGrid.Columns["MatchedSource"].Index).Trim();
                if (matchedSource.Equals("Tree Node", System.StringComparison.OrdinalIgnoreCase))
                    return;

                string propName = SafeCellText(row, searchResultsGrid.Columns["PropName"].Index).Trim();
                string manualPath = SafeCellText(row, searchResultsGrid.Columns["ManualPath"].Index).Trim();
                if (propName.Length == 0 || manualPath.Length == 0)
                    return;

                string header = searchResultsGrid.Columns[args.ColumnIndex].HeaderText;
                string newValueText = SafeCellText(row, args.ColumnIndex);

                try
                {
                    var targetNode = FindNodeByManualPath(tree, manualPath);
                    if (targetNode == null)
                    {
                        throw new System.Exception(
                            "Could not re-locate this hit's node (the tree may have changed since the search ran).");
                    }

                    SelectNode(targetNode);

                    var liveColumnMap = BuildColumnMap(dataGridView);
                    int liveNameCol = liveColumnMap.ContainsKey("Name")
                        ? liveColumnMap["Name"]
                        : (liveColumnMap.ContainsKey("Property Name") ? liveColumnMap["Property Name"] : -1);
                    int liveTargetCol = liveColumnMap.ContainsKey(header) ? liveColumnMap[header] : -1;

                    if (liveNameCol < 0 || liveTargetCol < 0)
                        throw new System.Exception("Could not find the '" + header + "' column on this node's live property grid.");

                    System.Windows.Forms.DataGridViewRow targetRow = null;
                    foreach (System.Windows.Forms.DataGridViewRow candidateRow in dataGridView.Rows)
                    {
                        if (candidateRow.IsNewRow) continue;

                        if (string.Equals(SafeCellText(candidateRow, liveNameCol).Trim(), propName, System.StringComparison.Ordinal))
                        {
                            targetRow = candidateRow;
                            break;
                        }
                    }

                    if (targetRow == null)
                        throw new System.Exception("Property '" + propName + "' was not found on this node's live property grid anymore.");

                    targetRow.Cells[liveTargetCol].Value = newValueText;

                    lastStatusText = "Live-edited '" + propName + "' (" + header + ") to '" + newValueText + "'.";
                    statusTextBox.Text = lastStatusText;
                }
                catch (System.Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(
                        "Live edit failed\r\n\r\n" + ex.Message,
                        "Edit Error",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Error);
                }
            };

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

                // Entry / child / target-term conditions: three equal columns spread evenly
                // across the width, instead of two stacked on the left and one on the right.
                // Each column follows the same row order (label, then textbox, then its
                // secondary controls) so all three textboxes land on the same Top.
                int columnsTop = margin;
                int columnWidth = (formWidth - (margin * 2) - (gap * 2)) / 3;
                int col1Left = margin;
                int col2Left = col1Left + columnWidth + gap;
                int col3Left = col2Left + columnWidth + gap;

                filtersLabel.Left = col1Left;
                filtersLabel.Top = columnsTop;
                filtersLabel.Width = columnWidth;
                filtersTextBox.Left = col1Left;
                filtersTextBox.Top = filtersLabel.Bottom + 4;
                filtersTextBox.Width = columnWidth;
                filtersTextBox.Height = 100;
                logicLabel.Left = col1Left;
                logicLabel.Top = filtersTextBox.Bottom + 8;
                logicLabel.Width = 90;
                logicComboBox.Left = logicLabel.Right + 6;
                logicComboBox.Top = filtersTextBox.Bottom + 4;
                logicComboBox.Width = columnWidth - (logicComboBox.Left - col1Left);
                selectedOnlyCheckBox.Left = col1Left;
                selectedOnlyCheckBox.Top = logicComboBox.Bottom + 8;
                selectedOnlyCheckBox.Width = columnWidth;

                childFiltersLabel.Left = col2Left;
                childFiltersLabel.Top = columnsTop;
                childFiltersLabel.Width = columnWidth;
                childFiltersTextBox.Left = col2Left;
                childFiltersTextBox.Top = childFiltersLabel.Bottom + 4;
                childFiltersTextBox.Width = columnWidth;
                childFiltersTextBox.Height = 100;
                childLogicLabel.Left = col2Left;
                childLogicLabel.Top = childFiltersTextBox.Bottom + 8;
                childLogicLabel.Width = 120;
                childLogicComboBox.Left = childLogicLabel.Right + 6;
                childLogicComboBox.Top = childFiltersTextBox.Bottom + 4;
                childLogicComboBox.Width = columnWidth - (childLogicComboBox.Left - col2Left);
                recursiveChildFilterCheckBox.Left = col2Left;
                recursiveChildFilterCheckBox.Top = childLogicComboBox.Bottom + 8;
                recursiveChildFilterCheckBox.Width = columnWidth;

                searchTermsLabel.Left = col3Left;
                searchTermsLabel.Top = columnsTop;
                searchTermsLabel.Width = columnWidth;
                searchTermsTextBox.Left = col3Left;
                searchTermsTextBox.Top = searchTermsLabel.Bottom + 4;
                searchTermsTextBox.Width = columnWidth;
                searchTermsTextBox.Height = 100;
                searchSelectedOnlyCheckBox.Left = col3Left;
                searchSelectedOnlyCheckBox.Top = searchTermsTextBox.Bottom + 8;
                searchSelectedOnlyCheckBox.Width = columnWidth;
                useSearchHitsAsScopeCheckBox.Left = col3Left;
                useSearchHitsAsScopeCheckBox.Top = searchSelectedOnlyCheckBox.Bottom + 6;
                useSearchHitsAsScopeCheckBox.Width = columnWidth;
                autoAddSearchPropsCheckBox.Left = col3Left;
                autoAddSearchPropsCheckBox.Top = useSearchHitsAsScopeCheckBox.Bottom + 6;
                autoAddSearchPropsCheckBox.Width = columnWidth;
                autoAddSearchEntriesCheckBox.Left = col3Left;
                autoAddSearchEntriesCheckBox.Top = autoAddSearchPropsCheckBox.Bottom + 6;
                autoAddSearchEntriesCheckBox.Width = columnWidth;

                int sectionBottom = System.Math.Max(
                    selectedOnlyCheckBox.Bottom,
                    System.Math.Max(recursiveChildFilterCheckBox.Bottom, autoAddSearchEntriesCheckBox.Bottom));
                int ruleTop = sectionBottom + 18;

                int buttonsTop = configForm.ClientSize.Height - 42;
                int statusTop = buttonsTop - 162;
                int searchGridHeight = 155;
                int searchGridGap = 12;
                int ruleSearchGap = 14;
                int searchGridTop = statusTop - searchGridGap - searchGridHeight;

                statusLabel.Left = margin;
                statusLabel.Top = statusTop;
                statusTextBox.Left = margin;
                statusTextBox.Top = statusTop + labelGap;
                statusTextBox.Width = formWidth - (margin * 2);
                statusTextBox.Height = buttonsTop - statusTextBox.Top - 14;

                rulesGrid.Left = margin;
                rulesGrid.Top = ruleTop;
                rulesGrid.Width = formWidth - (margin * 2);
                rulesGrid.Height = searchGridTop - ruleTop - ruleSearchGap;

                searchResultsGrid.Left = margin;
                searchResultsGrid.Top = searchGridTop;
                searchResultsGrid.Width = formWidth - (margin * 2);
                searchResultsGrid.Height = searchGridHeight;

                // Bottom button row: rule-management and search buttons grouped together on
                // the left (in click-order, left to right), primary actions pinned to the
                // right - Run Batch / Close keep their own Anchor-driven Left position.
                int buttonGap = 8;
                int cursorLeft = margin;

                void PlaceLeftButton(System.Windows.Forms.Button button)
                {
                    button.Left = cursorLeft;
                    button.Top = buttonsTop;
                    cursorLeft = button.Right + buttonGap;
                }

                PlaceLeftButton(addRowButton);
                PlaceLeftButton(removeRowButton);
                PlaceLeftButton(resetExamplesButton);
                PlaceLeftButton(searchButton);
                PlaceLeftButton(clearSearchResultsButton);
                PlaceLeftButton(addSelectedPropsButton);
                PlaceLeftButton(addSelectedEntriesButton);

                runButton.Top = buttonsTop;
                cancelButton.Top = buttonsTop;
            }

            configForm.Shown += (sender, args) => UpdateBottomLayout();
            configForm.Resize += (sender, args) => UpdateBottomLayout();

            // cancelButton.Click (above) is what lets Close interrupt a running search: it
            // sets cancelRequested and waits for the search loop to unwind on its own before
            // ever calling configForm.Close(). This handler is the backstop for the other
            // ways a modal dialog can still be torn down (title-bar X, Alt+F4) - it blocks
            // those outright while busy, since there's no cooperative-cancel path for them to
            // wait on, and the underlying loop is still iterating live controls/collections.
            configForm.FormClosing += (sender, args) =>
            {
                if (isBusy)
                {
                    args.Cancel = true;
                    return;
                }

                // The Run Batch path already saves before closing (Tag == "RUN"); this covers
                // Close/X/Escape, which otherwise never persisted the current settings.
                if (!object.Equals(configForm.Tag, "RUN"))
                {
                    try
                    {
                        rulesGrid.EndEdit();
                        searchResultsGrid.EndEdit();
                        SaveCurrentConfig();
                    }
                    catch
                    {
                        // Best effort - a save failure shouldn't block the user from closing.
                    }
                }
            };

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
            // Keyed by entry manual path (not entry name), so scope narrows to the exact
            // entry instance that was checked even when a same-named entry exists elsewhere.
            var searchHitScopeEntryPaths = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
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

                    var tempSearchScopeEntryPaths = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
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

                            string entryManualPath = SafeCellText(row, searchResultsGrid.Columns["EntryManualPath"].Index).Trim();
                            if (!string.IsNullOrWhiteSpace(entryManualPath))
                                tempSearchScopeEntryPaths.Add(entryManualPath);
                        }

                        if (tempSearchScopeEntryPaths.Count == 0)
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

                        string valueColumnHeader = row.Cells["ValueColumn"].Value == null
                            ? defaultValueColumnHeader
                            : row.Cells["ValueColumn"].Value.ToString().Trim();

                        if (string.IsNullOrWhiteSpace(valueColumnHeader) || !valueColumnHeaders.Contains(valueColumnHeader))
                            valueColumnHeader = defaultValueColumnHeader;

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
                        rule["ValueColumnHeader"] = valueColumnHeader;

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
                    searchHitScopeEntryPaths = tempSearchScopeEntryPaths;
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
                int skippedByMissingColumns = 0;
                int skippedDivByZero = 0;
                int skippedRowsMissingValueColumn = 0;
                int failedEntryCount = 0;
                int rowsMatchedByRule = 0;
                var failedEntryDetails = new System.Collections.Generic.List<string>();

                var targetPropertyNames =
                    new System.Collections.Generic.HashSet<string>(
                        propertyRuleMap.Keys,
                        System.StringComparer.OrdinalIgnoreCase);

                foreach (System.Windows.Forms.TreeNode entryNode in entryNodes)
                {
                    string entryName = entryNode.Text ?? string.Empty;

                    if (useSearchHitsScope && !searchHitScopeEntryPaths.Contains(BuildManualNodePath(entryNode)))
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

                    // Each entry is processed in its own try/catch: a failure on one entry
                    // (e.g. an unexpected grid layout) is recorded and skipped rather than
                    // aborting the whole batch and leaving prior edits applied with no report
                    // of what wasn't reached.
                    try
                    {
                        var editableNode = ResolveBestEditableNode(
                            entryNode,
                            dataGridView,
                            nameColumnIndex,
                            targetPropertyNames);

                        SelectNode(editableNode);

                        // Column layout is NOT assumed to be identical across entries/export
                        // types (structs, DataTable rows, etc. can differ). Re-resolve the
                        // Name/Is Zero columns against the grid now actually showing; each
                        // rule's own value column is resolved from entryColumnMap per-row inside
                        // ProcessEntryRows, since different rules can target different columns.
                        var entryColumnMap = BuildColumnMap(dataGridView);
                        int entryNameColumnIndex = entryColumnMap.ContainsKey("Name")
                            ? entryColumnMap["Name"]
                            : (entryColumnMap.ContainsKey("Property Name") ? entryColumnMap["Property Name"] : -1);
                        int entryIsZeroColumnIndex = entryColumnMap.ContainsKey("Is Zero") ? entryColumnMap["Is Zero"] : -1;

                        if (entryNameColumnIndex < 0)
                        {
                            skippedEntries++;
                            skippedByMissingColumns++;
                            failedEntryDetails.Add(entryName + ": Name column not found for this entry's grid layout.");
                        }
                        else
                        {
                            bool changedThisEntry = ProcessEntryRows(
                                dataGridView,
                                entryNameColumnIndex,
                                entryColumnMap,
                                entryIsZeroColumnIndex,
                                propertyRuleMap,
                                ref rowsMatchedByRule,
                                ref editedValues,
                                ref editedIsZeroFlags,
                                ref skippedDivByZero,
                                ref skippedRowsMissingValueColumn);

                            if (changedThisEntry)
                                editedEntries++;
                        }
                    }
                    catch (System.Exception entryEx)
                    {
                        failedEntryCount++;
                        failedEntryDetails.Add(entryName + ": " + entryEx.Message);
                    }
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
                    "Skipped by search scope: " + skippedBySearchScope + "\r\n" +
                    "Skipped, missing Name column: " + skippedByMissingColumns + "\r\n" +
                    "Skipped rows, missing chosen value column: " + skippedRowsMissingValueColumn + "\r\n" +
                    "Skipped writes, divide by zero: " + skippedDivByZero + "\r\n" +
                    "Entries that threw errors: " + failedEntryCount +
                    (failedEntryDetails.Count == 0
                        ? string.Empty
                        : "\r\n\r\nEntry errors (first 15):\r\n" +
                          string.Join("\r\n", failedEntryDetails.GetRange(0, System.Math.Min(15, failedEntryDetails.Count)).ToArray()));

                System.Windows.Forms.MessageBox.Show(
                    "Done.\r\n\r\n" +
                    "Edited values: " + editedValues + "\r\n" +
                    "Edited entries: " + editedEntries +
                    (failedEntryCount > 0
                        ? "\r\n\r\n" + failedEntryCount + " entr" + (failedEntryCount == 1 ? "y" : "ies") +
                          " threw an error and were skipped; see the status box for details."
                        : string.Empty),
                    "Batch Edit Complete",
                    failedEntryCount > 0
                        ? System.Windows.Forms.MessageBoxButtons.OK
                        : System.Windows.Forms.MessageBoxButtons.OK,
                    failedEntryCount > 0
                        ? System.Windows.Forms.MessageBoxIcon.Warning
                        : System.Windows.Forms.MessageBoxIcon.Information);

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
