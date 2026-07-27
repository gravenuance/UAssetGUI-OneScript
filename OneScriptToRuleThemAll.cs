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
        var dgv = dgvField.GetValue(form) as System.Windows.Forms.DataGridView;

        if (tree == null || dgv == null)
        {
            System.Windows.Forms.MessageBox.Show(
                "treeView1 or dataGridView1 is null.",
                "Script Error");
            return;
        }

        int nameCol = -1;
        int valueCol = -1;
        int isZeroCol = -1;

        for (int columnIndex = 0; columnIndex < dgv.Columns.Count; columnIndex++)
        {
            string headerText = dgv.Columns[columnIndex].HeaderText;

            if (headerText == "Name") nameCol = columnIndex;
            if (headerText == "Value") valueCol = columnIndex;
            if (headerText == "Is Zero") isZeroCol = columnIndex;
        }

        if (nameCol < 0 || valueCol < 0)
        {
            System.Windows.Forms.MessageBox.Show(
                "Could not find Name/Value columns in dataGridView1.",
                "Script Error");
            return;
        }

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

        bool ContainsToken(string source, string token)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(token))
                return false;

            return source.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        bool EntryMatchesConditions(
            string entryName,
            System.Collections.Generic.IEnumerable<string> conditions,
            bool useAndLogic)
        {
            if (string.IsNullOrWhiteSpace(entryName))
                return false;

            var filteredConditions = new System.Collections.Generic.List<string>();
            foreach (string condition in conditions)
            {
                if (!string.IsNullOrWhiteSpace(condition))
                    filteredConditions.Add(condition);
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

        string FormatFloat(float value)
        {
            return System.Math.Round(value, 6).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        bool IsNullLikeByRule(string valueType, object value)
        {
            if (value == null)
                return true;

            switch (valueType)
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
                    string text = value.ToString();
                    return string.IsNullOrWhiteSpace(text);
                }
                default:
                    return value == null;
            }
        }

        bool IsGenericNullLike(object value)
        {
            if (value == null)
                return true;

            string text = value.ToString().Trim();

            if (text.Length == 0)
                return true;

            if (text.Equals("false", System.StringComparison.OrdinalIgnoreCase))
                return true;

            if (text == "0")
                return true;

            float parsedFloat;
            if (TryParseFloat(value, out parsedFloat) && System.Math.Abs(parsedFloat) < 0.000001f)
                return true;

            return false;
        }

        bool EvaluateSkipNumeric(string skipOperation, float currentValue, float skipValue)
        {
            switch ((skipOperation ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "eq":
                    return System.Math.Abs(currentValue - skipValue) < 0.000001f;
                case "lt":
                case "smaller":
                case "smallerthan":
                case "smaller_than":
                    return currentValue < skipValue;
                case "gt":
                case "bigger":
                case "biggerthan":
                case "bigger_than":
                    return currentValue > skipValue;
                case "lte":
                case "le":
                    return currentValue <= skipValue;
                case "gte":
                case "ge":
                    return currentValue >= skipValue;
                default:
                    return false;
            }
        }

        int ApplyIntOperation(int currentValue, string operation, int targetValue)
        {
            switch ((operation ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "set":
                    return targetValue;
                case "add":
                    return currentValue + targetValue;
                case "sub":
                case "subtract":
                    return currentValue - targetValue;
                case "mul":
                case "multiply":
                    return currentValue * targetValue;
                case "div":
                case "divide":
                    return targetValue == 0 ? currentValue : currentValue / targetValue;
                default:
                    return currentValue;
            }
        }

        float ApplyFloatOperation(float currentValue, string operation, float targetValue)
        {
            switch ((operation ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "set":
                    return targetValue;
                case "add":
                    return currentValue + targetValue;
                case "sub":
                case "subtract":
                    return currentValue - targetValue;
                case "mul":
                case "multiply":
                    return currentValue * targetValue;
                case "div":
                case "divide":
                    return System.Math.Abs(targetValue) < 0.000001f ? currentValue : currentValue / targetValue;
                default:
                    return currentValue;
            }
        }

        var entryNameConditions = new System.Collections.Generic.List<string>
        {
            "Name1",
            "Name2"
        };

        bool useAndLogicForEntryName = true;

        var propertyRules = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>
        {
            new System.Collections.Generic.Dictionary<string, object>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "PropName", "BoolExample" },
                { "Type", "bool" },
                { "TargetValue", true },
                { "SkipValue", true }
            },
            new System.Collections.Generic.Dictionary<string, object>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "PropName", "FloatExample" },
                { "Type", "float" },
                { "TargetOperation", "mul" },
                { "TargetValue", 0.9f },
                { "SkipOperation", "eq" },
                { "SkipValue", 0f }
            },
            new System.Collections.Generic.Dictionary<string, object>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "PropName", "StringExample" },
                { "Type", "string" },
                { "TargetValue", "NewValue" },
                { "SkipValue", "NewValue" }
            }
        };

        var propertyRuleMap =
            new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, object>>(
                System.StringComparer.OrdinalIgnoreCase);

        foreach (var rule in propertyRules)
        {
            if (!rule.ContainsKey("PropName")) continue;

            string propName = (rule["PropName"] ?? string.Empty).ToString().Trim();
            if (propName.Length == 0) continue;

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
            System.Windows.Forms.MessageBox.Show(
                "Could not find 'Export Data' node.",
                "Script Error");
            return;
        }

        SelectNode(exportDataNode);
        exportDataNode.Expand();

        var export1Node = FindChildNode(exportDataNode, "Export 1");
        if (export1Node == null)
        {
            System.Windows.Forms.MessageBox.Show(
                "Could not find 'Export 1' node.",
                "Script Error");
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
            System.Windows.Forms.MessageBox.Show(
                "Could not find 'Table Info' node under Export 1.",
                "Script Error");
            return;
        }

        SelectNode(tableInfoNode);
        tableInfoNode.Expand();

        int matchedEntries = 0;
        int editedEntries = 0;
        int editedValues = 0;
        int zeroFlagEdits = 0;
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

            foreach (System.Windows.Forms.DataGridViewRow row in dgv.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Cells[nameCol] == null || row.Cells[valueCol] == null) continue;

                object nameObject = row.Cells[nameCol].Value;
                if (nameObject == null) continue;

                string propName = nameObject.ToString().Trim();
                if (!propertyRuleMap.ContainsKey(propName)) continue;

                var rule = propertyRuleMap[propName];
                object valueObject = row.Cells[valueCol].Value;

                string type = rule.ContainsKey("Type")
                    ? (rule["Type"] ?? string.Empty).ToString().Trim().ToLowerInvariant()
                    : string.Empty;

                bool shouldSetIsZero = IsNullLikeByRule(type, valueObject) || IsGenericNullLike(valueObject);

                if (shouldSetIsZero && isZeroCol >= 0 && row.Cells[isZeroCol] != null)
                {
                    object currentIsZero = row.Cells[isZeroCol].Value;
                    bool currentIsZeroBool;
                    bool isAlreadyTrue = TryParseBool(currentIsZero, out currentIsZeroBool) && currentIsZeroBool;

                    if (!isAlreadyTrue)
                    {
                        row.Cells[isZeroCol].Value = "True";
                        zeroFlagEdits++;
                        changedThisEntry = true;
                    }
                }

                switch (type)
                {
                    case "bool":
                    {
                        bool currentValue;
                        if (!TryParseBool(valueObject, out currentValue)) break;

                        bool targetValue = rule.ContainsKey("TargetValue") && rule["TargetValue"] != null
                            ? System.Convert.ToBoolean(rule["TargetValue"])
                            : currentValue;

                        bool hasSkipValue = rule.ContainsKey("SkipValue") && rule["SkipValue"] != null;
                        if (hasSkipValue)
                        {
                            bool skipValue = System.Convert.ToBoolean(rule["SkipValue"]);
                            if (currentValue == skipValue)
                                break;
                        }

                        if (currentValue != targetValue)
                        {
                            row.Cells[valueCol].Value = targetValue ? "True" : "False";
                            editedValues++;
                            changedThisEntry = true;
                        }

                        break;
                    }

                    case "string":
                    {
                        string currentValue = valueObject == null ? string.Empty : valueObject.ToString();
                        string targetValue = rule.ContainsKey("TargetValue") && rule["TargetValue"] != null
                            ? rule["TargetValue"].ToString()
                            : currentValue;

                        bool hasSkipValue = rule.ContainsKey("SkipValue") && rule["SkipValue"] != null;
                        if (hasSkipValue)
                        {
                            string skipValue = rule["SkipValue"].ToString();
                            if (string.Equals(currentValue, skipValue, System.StringComparison.OrdinalIgnoreCase))
                                break;
                        }

                        if (!string.Equals(currentValue, targetValue, System.StringComparison.Ordinal))
                        {
                            row.Cells[valueCol].Value = targetValue;
                            editedValues++;
                            changedThisEntry = true;
                        }

                        break;
                    }

                    case "int":
                    {
                        int currentValue;
                        if (!TryParseInt(valueObject, out currentValue)) break;

                        string targetOperation = rule.ContainsKey("TargetOperation")
                            ? (rule["TargetOperation"] ?? string.Empty).ToString()
                            : "set";

                        int targetValue = rule.ContainsKey("TargetValue") && rule["TargetValue"] != null
                            ? System.Convert.ToInt32(rule["TargetValue"])
                            : currentValue;

                        bool hasSkipValue = rule.ContainsKey("SkipValue") && rule["SkipValue"] != null;
                        if (hasSkipValue)
                        {
                            string skipOperation = rule.ContainsKey("SkipOperation")
                                ? (rule["SkipOperation"] ?? string.Empty).ToString()
                                : "eq";

                            int skipValue = System.Convert.ToInt32(rule["SkipValue"]);
                            if (EvaluateSkipNumeric(skipOperation, currentValue, skipValue))
                                break;
                        }

                        int newValue = ApplyIntOperation(currentValue, targetOperation, targetValue);
                        if (newValue != currentValue)
                        {
                            row.Cells[valueCol].Value = newValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                            editedValues++;
                            changedThisEntry = true;
                        }

                        break;
                    }

                    case "float":
                    {
                        float currentValue;
                        if (!TryParseFloat(valueObject, out currentValue)) break;

                        string targetOperation = rule.ContainsKey("TargetOperation")
                            ? (rule["TargetOperation"] ?? string.Empty).ToString()
                            : "set";

                        float targetValue = rule.ContainsKey("TargetValue") && rule["TargetValue"] != null
                            ? System.Convert.ToSingle(rule["TargetValue"], System.Globalization.CultureInfo.InvariantCulture)
                            : currentValue;

                        bool hasSkipValue = rule.ContainsKey("SkipValue") && rule["SkipValue"] != null;
                        if (hasSkipValue)
                        {
                            string skipOperation = rule.ContainsKey("SkipOperation")
                                ? (rule["SkipOperation"] ?? string.Empty).ToString()
                                : "eq";

                            float skipValue = System.Convert.ToSingle(rule["SkipValue"], System.Globalization.CultureInfo.InvariantCulture);
                            if (EvaluateSkipNumeric(skipOperation, currentValue, skipValue))
                                break;
                        }

                        float newValue = ApplyFloatOperation(currentValue, targetOperation, targetValue);
                        if (System.Math.Abs(newValue - currentValue) >= 0.000001f)
                        {
                            row.Cells[valueCol].Value = FormatFloat(newValue);
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

        string logicLabel = useAndLogicForEntryName ? "AND" : "OR";

        System.Windows.Forms.MessageBox.Show(
            "Done.\n\n" +
            "Entry name logic: " + logicLabel + "\n" +
            "Entry conditions: " + string.Join(", ", entryNameConditions) + "\n" +
            "Configured prop rules: " + propertyRuleMap.Count + "\n\n" +
            "Matched entries: " + matchedEntries + "\n" +
            "Edited entries: " + editedEntries + "\n" +
            "Edited values: " + editedValues + "\n" +
            "Edited Is Zero flags: " + zeroFlagEdits + "\n" +
            "Skipped entries: " + skippedEntries,
            "Batch Edit Complete");
    }
    catch (System.Exception ex)
    {
        System.Windows.Forms.MessageBox.Show(ex.ToString(), "Script Error");
    }
});