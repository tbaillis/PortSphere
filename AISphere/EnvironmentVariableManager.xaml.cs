using System;
using System.Collections;
using System.Windows;

namespace AISphere
{
    public partial class EnvironmentVariableManager : Window
    {
    private readonly string[] AllowedKeys = { "OPENAI_API_KEY", "MCP_SERVER_URL" };

        public EnvironmentVariableManager()
        {
            InitializeComponent();
            RefreshVariableList();
            PopulateAllowedKeys();
        }

        private void PopulateAllowedKeys()
        {
            VariableNameComboBox.Items.Clear();
            foreach (var key in AllowedKeys)
            {
                VariableNameComboBox.Items.Add(key);
            }
            VariableNameComboBox.SelectedIndex = 0;
        }

        private void SetButton_Click(object sender, RoutedEventArgs e)
        {
            var name = VariableNameComboBox.SelectedItem?.ToString();
            var value = VariableValue.Text;

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please select a variable name.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Debug log to verify the variable being set
            Console.WriteLine($"Setting environment variable: {name} = {value}");

            // Persist for the user and also set for the current process so the running app can read it immediately
            Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);
            MessageBox.Show($"Environment variable '{name}' set successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshVariableList();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var name = VariableNameComboBox.SelectedItem?.ToString();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please select a variable name.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Remove from both user profile and current process
            Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.Process);
            MessageBox.Show($"Environment variable '{name}' deleted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshVariableList();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshVariableList();
        }

        private void RefreshVariableList()
        {
            VariableList.Items.Clear();
            var variables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User);

            foreach (var key in AllowedKeys)
            {
                if (variables.Contains(key))
                {
                    VariableList.Items.Add($"{key} = {variables[key]}");
                }
            }
        }
    }
}
