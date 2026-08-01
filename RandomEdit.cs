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
        var propertyGrid = dgvField.GetValue(form) as System.Windows.Forms.DataGridView;

        if (tree == null || propertyGrid == null)
        {
            System.Windows.Forms.MessageBox.Show(
                "treeView1 or dataGridView1 is null.",
                "Script Error");
            return;
        }

        bool stopRequested = false;
        bool passRunning = false;

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
            if (node == null || stopRequested) return;

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

        bool ContainsToken(string source, string token)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(token))
                return false;

            return source.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        bool ContainsAnyToken(string source, string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(source))
                return false;

            foreach (string token in tokens)
            {
                if (ContainsToken(source, token))
                    return true;
            }

            return false;
        }

        string SafeCellText(System.Windows.Forms.DataGridViewRow row, int index)
        {
            if (row == null) return string.Empty;
            if (index < 0 || index >= row.Cells.Count) return string.Empty;
            if (row.Cells[index] == null) return string.Empty;

            object value = row.Cells[index].Value;
            return value == null ? string.Empty : value.ToString().Trim();
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

        int ClampInt(int value, int minValue, int maxValue)
        {
            if (value < minValue) return minValue;
            if (value > maxValue) return maxValue;
            return value;
        }

        int ComputePostSuccessPenaltyDenominator(int baseDenominator)
        {
            baseDenominator = System.Math.Max(1, baseDenominator);

            // Balanced default: the next hit is significantly harder,
            // but not so harsh that the system feels frozen.
            return System.Math.Max(baseDenominator + 2, (baseDenominator * 2) + 1);
        }

        int ComputeCurrentDynamicDenominator(
            int baseDenominator,
            int failedEligibleAttemptsSinceLastSuccess)
        {
            int penaltyDenominator = ComputePostSuccessPenaltyDenominator(baseDenominator);

            // Balanced pity recovery: each failed eligible attempt lowers the denominator,
            // reaching guaranteed hit after a few misses instead of immediately.
            int pityStep = System.Math.Max(1, (baseDenominator + 1) / 2);
            int reduced = penaltyDenominator - (failedEligibleAttemptsSinceLastSuccess * pityStep);

            return ClampInt(reduced, 1, penaltyDenominator);
        }

        string PickMiniReplacement(string oldValue, System.Random rng)
        {
            if (ContainsToken(oldValue, "GrubDash"))
                return "M_GrubShooterElite_Mini";

            if (ContainsToken(oldValue, "Bot") || ContainsToken(oldValue, "Droid"))
                return "M_WeaponMaster_Mini";

            if (ContainsToken(oldValue, "HedgeBoar"))
                return "M_HedgeBoarBrute_Mini";

            int roll10 = rng.Next(1, 5);

            switch (roll10)
            {
                case 1: return "M_GrubShooterElite_Mini";
                case 2: return "M_WeaponMaster_Mini";
                case 3: return "M_HedgeBoarBrute_Mini";
                case 4: return "M_HedgeBoarBrute_Mini";
                default: return "M_WeaponMaster_Mini";
            }
        }

        bool IsProtectedAlias(string value)
        {
            return ContainsToken(value, "RoadBlock")
                || ContainsToken(value, "Tentacle")
                || ContainsToken(value, "turret")
                || ContainsToken(value, "Raven")
                || ContainsToken(value, "Marionette")
                || ContainsToken(value, "Behemoth")
                || ContainsToken(value, "Sawshark")
                || ContainsToken(value, "Opener")
                || ContainsToken(value, "Gorilla")
                || ContainsToken(value, "HedgeBoarBrute")
                || ContainsToken(value, "SkullJuggernaut")
                || ContainsToken(value, "GrubShooterElite")
                || ContainsToken(value, "Crawler")
                || ContainsToken(value, "WeaponMaster")
                || ContainsToken(value, "Statue")
                || ContainsToken(value, "Hydra")
                || ContainsToken(value, "RoyalGuard")
                || ContainsToken(value, "UnderGround")
                || ContainsToken(value, "Nikke");
        }

        bool TryFindCharacterAliasRow(
            out System.Windows.Forms.DataGridViewRow matchedRow,
            out int targetColumnIndex,
            out string targetColumnName,
            out string currentAlias,
            out string matchReason)
        {
            matchedRow = null;
            targetColumnIndex = -1;
            targetColumnName = string.Empty;
            currentAlias = string.Empty;
            matchReason = string.Empty;

            var columnMap = BuildColumnMap(propertyGrid);

            int nameCol = columnMap.ContainsKey("Name") ? columnMap["Name"] : -1;
            int typeCol = columnMap.ContainsKey("Type") ? columnMap["Type"] : -1;
            int variantCol = columnMap.ContainsKey("Variant") ? columnMap["Variant"] : -1;
            int valueCol = columnMap.ContainsKey("Value") ? columnMap["Value"] : -1;

            foreach (System.Windows.Forms.DataGridViewRow row in propertyGrid.Rows)
            {
                if (stopRequested)
                    return false;

                if (row.IsNewRow)
                    continue;

                string rowName = SafeCellText(row, nameCol);
                string rowType = SafeCellText(row, typeCol);
                string rowVariant = SafeCellText(row, variantCol);
                string rowValue = SafeCellText(row, valueCol);

                bool looksLikeAliasRow =
                    string.Equals(rowType, "NameProperty", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(rowName, "0", System.StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(rowName);

                if (!looksLikeAliasRow)
                    continue;

                if (!string.IsNullOrWhiteSpace(rowVariant))
                {
                    matchedRow = row;
                    targetColumnIndex = variantCol;
                    targetColumnName = "Variant";
                    currentAlias = rowVariant;
                    matchReason = "Using Variant";
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(rowValue))
                {
                    matchedRow = row;
                    targetColumnIndex = valueCol;
                    targetColumnName = "Value";
                    currentAlias = rowValue;
                    matchReason = "Using Value";
                    return true;
                }
            }

            return false;
        }

        var exportDataNode = (System.Windows.Forms.TreeNode)null;
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

        var controlForm = new System.Windows.Forms.Form();
        controlForm.Text = "CharacterAlias Batch Editor";
        controlForm.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        controlForm.Width = 1560;
        controlForm.Height = 760;
        controlForm.MinimizeBox = false;
        controlForm.ShowInTaskbar = false;
        controlForm.Owner = form;

        var chanceLabel = new System.Windows.Forms.Label();
        chanceLabel.Left = 12;
        chanceLabel.Top = 15;
        chanceLabel.Width = 120;
        chanceLabel.Text = "Edit chance: 1 in";

        var chanceNumeric = new System.Windows.Forms.NumericUpDown();
        chanceNumeric.Left = 130;
        chanceNumeric.Top = 12;
        chanceNumeric.Width = 70;
        chanceNumeric.Minimum = 1;
        chanceNumeric.Maximum = 1000;
        chanceNumeric.Value = 5;

        var previewButton = new System.Windows.Forms.Button();
        previewButton.Left = 220;
        previewButton.Top = 10;
        previewButton.Width = 100;
        previewButton.Height = 28;
        previewButton.Text = "Preview";

        var applyButton = new System.Windows.Forms.Button();
        applyButton.Left = 326;
        applyButton.Top = 10;
        applyButton.Width = 100;
        applyButton.Height = 28;
        applyButton.Text = "Apply";

        var closeButton = new System.Windows.Forms.Button();
        closeButton.Left = 432;
        closeButton.Top = 10;
        closeButton.Width = 100;
        closeButton.Height = 28;
        closeButton.Text = "Close";
        closeButton.Click += (s, e) =>
        {
            stopRequested = true;
            if (!passRunning)
                controlForm.Close();
        };

        var statusLabel = new System.Windows.Forms.Label();
        statusLabel.Left = 550;
        statusLabel.Top = 15;
        statusLabel.Width = 980;
        statusLabel.Height = 24;
        statusLabel.Text = "Ready.";

        var resultsGrid = new System.Windows.Forms.DataGridView();
        resultsGrid.Left = 12;
        resultsGrid.Top = 48;
        resultsGrid.Width = 1520;
        resultsGrid.Height = 660;
        resultsGrid.Anchor =
            System.Windows.Forms.AnchorStyles.Top |
            System.Windows.Forms.AnchorStyles.Bottom |
            System.Windows.Forms.AnchorStyles.Left |
            System.Windows.Forms.AnchorStyles.Right;
        resultsGrid.ReadOnly = true;
        resultsGrid.AllowUserToAddRows = false;
        resultsGrid.AllowUserToDeleteRows = false;
        resultsGrid.AllowUserToResizeRows = false;
        resultsGrid.MultiSelect = false;
        resultsGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
        resultsGrid.RowHeadersVisible = false;
        resultsGrid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.None;

        resultsGrid.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn() { Name = "Entry", HeaderText = "Entry", Width = 180 });
        resultsGrid.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn() { Name = "AliasNode", HeaderText = "AliasNode", Width = 150 });
        resultsGrid.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn() { Name = "SourceColumn", HeaderText = "SourceColumn", Width = 110 });
        resultsGrid.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn() { Name = "CurrentValue", HeaderText = "CurrentValue", Width = 220 });
        resultsGrid.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn() { Name = "Eligible", HeaderText = "Eligible", Width = 70 });
        resultsGrid.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn() { Name = "Reason", HeaderText = "Reason", Width = 150 });
        resultsGrid.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn() { Name = "Roll", HeaderText = "Roll", Width = 180 });
        resultsGrid.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn() { Name = "Target", HeaderText = "Target", Width = 220 });
        resultsGrid.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn() { Name = "Action", HeaderText = "Action", Width = 90 });
        resultsGrid.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn() { Name = "Lookup", HeaderText = "Lookup", Width = 120 });
        resultsGrid.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn() { Name = "NodePath", HeaderText = "NodePath", Width = 470 });

        controlForm.Controls.Add(chanceLabel);
        controlForm.Controls.Add(chanceNumeric);
        controlForm.Controls.Add(previewButton);
        controlForm.Controls.Add(applyButton);
        controlForm.Controls.Add(closeButton);
        controlForm.Controls.Add(statusLabel);
        controlForm.Controls.Add(resultsGrid);

        controlForm.FormClosing += (s, e) =>
        {
            stopRequested = true;

            if (passRunning)
            {
                e.Cancel = true;
                statusLabel.Text = "Stopping...";
            }
        };

        void FinishStopIfRequested()
        {
            if (stopRequested && !controlForm.IsDisposed)
                controlForm.Close();
        }

        void RunPass(bool applyChanges)
        {
            if (passRunning)
                return;

            stopRequested = false;
            passRunning = true;
            previewButton.Enabled = false;
            applyButton.Enabled = false;
            chanceNumeric.Enabled = false;
            resultsGrid.Rows.Clear();

            try
            {
                int chanceDenominator = (int)chanceNumeric.Value;
                var rng = new System.Random();
                string[] includeTokens = new string[] { "DED", "WLA", "WLB", "SE", "ME_01", "ME_02", "ME_03" };
                int failedEligibleAttemptsSinceLastSuccess = 0;
                bool previousEligibleEntryChanged = false;

                int matchedEntries = 0;
                int eligibleAliases = 0;
                int changedAliases = 0;
                int skippedBossEntries = 0;
                int skippedNoAliasNode = 0;
                int skippedNoAliasValue = 0;
                int skippedNonMonsterAlias = 0;
                int skippedProtectedAliases = 0;

                foreach (System.Windows.Forms.TreeNode entryNode in tableInfoNode.Nodes)
                {
                    if (stopRequested)
                        break;

                    string entryName = entryNode.Text ?? string.Empty;

                    if (!ContainsAnyToken(entryName, includeTokens))
                        continue;

                    if (ContainsToken(entryName, "Boss"))
                    {
                        skippedBossEntries++;
                        continue;
                    }

                    matchedEntries++;

                    SelectNode(entryNode);
                    if (stopRequested) break;
                    entryNode.Expand();

                    var characterAliasNode = FindChildNode(entryNode, "CharacterAlias");
                    if (characterAliasNode == null)
                    {
                        skippedNoAliasNode++;
                        continue;
                    }

                    SelectNode(characterAliasNode);
                    if (stopRequested) break;

                    System.Windows.Forms.DataGridViewRow aliasRow;
                    int targetColumnIndex;
                    string targetColumnName;
                    string currentAlias;
                    string lookupReason;

                    if (!TryFindCharacterAliasRow(
                        out aliasRow,
                        out targetColumnIndex,
                        out targetColumnName,
                        out currentAlias,
                        out lookupReason))
                    {
                        if (stopRequested)
                            break;

                        skippedNoAliasValue++;

                        previousEligibleEntryChanged = false;

                        resultsGrid.Rows.Add(
                            entryName,
                            characterAliasNode.Text,
                            "",
                            "",
                            "No",
                            "No alias value found",
                            "",
                            "",
                            "Skip",
                            "No match",
                            BuildManualNodePath(characterAliasNode));

                        System.Windows.Forms.Application.DoEvents();
                        continue;
                    }

                    if (!ContainsToken(currentAlias, "M_"))
                    {
                        skippedNonMonsterAlias++;

                        previousEligibleEntryChanged = false;

                        resultsGrid.Rows.Add(
                            entryName,
                            characterAliasNode.Text,
                            targetColumnName,
                            currentAlias,
                            "No",
                            "Alias is not M_",
                            "",
                            "",
                            "Skip",
                            lookupReason,
                            BuildManualNodePath(characterAliasNode));

                        System.Windows.Forms.Application.DoEvents();
                        continue;
                    }

                    bool protectedAlias = IsProtectedAlias(currentAlias);
                    bool isGrubDash = ContainsToken(currentAlias, "GrubDash");

                    int roll = 0;
                    bool rollHits = false;
                    string rollText = string.Empty;
                    string targetValue = string.Empty;
                    string action = "Skip";
                    string reason = "Roll miss";

                    if (protectedAlias)
                    {
                        skippedProtectedAliases++;
                        reason = "Protected alias";
                        previousEligibleEntryChanged = false;
                    }
                    else
                    {
                        eligibleAliases++;
                        targetValue = PickMiniReplacement(currentAlias, rng);

                        int currentDenominator;

                        if (isGrubDash)
                        {
                            currentDenominator = 3;
                        }
                        else
                        {
                            currentDenominator = ComputeCurrentDynamicDenominator(
                                chanceDenominator,
                                failedEligibleAttemptsSinceLastSuccess);
                        }

                        roll = rng.Next(1, currentDenominator + 1);
                        rollHits = roll == currentDenominator || currentDenominator <= 1;
                        rollText =
                            roll.ToString() + " / " + currentDenominator.ToString() +
                            "  [base " + chanceDenominator.ToString() +
                            ", failed eligible since last hit " + failedEligibleAttemptsSinceLastSuccess.ToString() + "]";

                        if (rollHits)
                        {
                            if (previousEligibleEntryChanged)
                            {
                                reason = "Adjacent change blocked";
                                action = "Skip";

                                if (!isGrubDash)
                                    failedEligibleAttemptsSinceLastSuccess++;

                                previousEligibleEntryChanged = false;
                            }
                            else
                            {
                                reason = isGrubDash ? "GrubDash roll hit" : "Dynamic roll hit";
                                action = applyChanges ? "Changed" : "Preview";

                                if (applyChanges && aliasRow != null && targetColumnIndex >= 0)
                                {
                                    aliasRow.Cells[targetColumnIndex].Value = targetValue;
                                    changedAliases++;
                                }

                                if (!isGrubDash)
                                    failedEligibleAttemptsSinceLastSuccess = 0;

                                previousEligibleEntryChanged = true;
                            }
                        }
                        else
                        {
                            reason = isGrubDash ? "GrubDash roll miss" : "Dynamic roll miss";

                            if (!isGrubDash)
                                failedEligibleAttemptsSinceLastSuccess++;

                            previousEligibleEntryChanged = false;
                        }
                    }

                    resultsGrid.Rows.Add(
                        entryName,
                        characterAliasNode.Text,
                        targetColumnName,
                        currentAlias,
                        protectedAlias ? "No" : "Yes",
                        reason,
                        rollText,
                        targetValue,
                        action,
                        lookupReason,
                        BuildManualNodePath(characterAliasNode));

                    statusLabel.Text =
                        (applyChanges ? "Applying..." : "Previewing...") +
                        " Entries: " + matchedEntries +
                        " | Eligible aliases: " + eligibleAliases +
                        " | Changed: " + changedAliases +
                        " | Failed since last hit: " + failedEligibleAttemptsSinceLastSuccess +
                        " | Stopping: " + (stopRequested ? "Yes" : "No");

                    System.Windows.Forms.Application.DoEvents();
                }

                if (stopRequested)
                {
                    statusLabel.Text =
                        "Stopped." +
                        " Matched entries: " + matchedEntries +
                        " | Eligible aliases: " + eligibleAliases +
                        " | Changed: " + changedAliases +
                        " | Boss skipped: " + skippedBossEntries +
                        " | No CharacterAlias: " + skippedNoAliasNode +
                        " | No alias value: " + skippedNoAliasValue +
                        " | Not M_: " + skippedNonMonsterAlias +
                        " | Protected skipped: " + skippedProtectedAliases;
                }
                else
                {
                    statusLabel.Text =
                        (applyChanges ? "Apply complete." : "Preview complete.") +
                        " Matched entries: " + matchedEntries +
                        " | Eligible aliases: " + eligibleAliases +
                        " | Changed: " + changedAliases +
                        " | Boss skipped: " + skippedBossEntries +
                        " | No CharacterAlias: " + skippedNoAliasNode +
                        " | No alias value: " + skippedNoAliasValue +
                        " | Not M_: " + skippedNonMonsterAlias +
                        " | Protected skipped: " + skippedProtectedAliases;
                }
            }
            finally
            {
                passRunning = false;

                if (!controlForm.IsDisposed)
                {
                    previewButton.Enabled = true;
                    applyButton.Enabled = true;
                    chanceNumeric.Enabled = true;
                }

                FinishStopIfRequested();
            }
        }

        previewButton.Click += (s, e) => RunPass(false);
        applyButton.Click += (s, e) => RunPass(true);

        controlForm.ShowDialog(form);
    }
    catch (System.Exception ex)
    {
        System.Windows.Forms.MessageBox.Show(ex.ToString(), "Script Error");
    }
});
