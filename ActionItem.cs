/* *******************************************************************************************************************
 * Application: ChatGPTExtension
 * 
 * Autor:  Daniel Liedke
 * 
 * Copyright © Daniel Liedke 2023
 * Usage and reproduction in any manner whatsoever without the written permission of Daniel Liedke is strictly forbidden.
 *  
 * Purpose: ActionItem class is used to store custom actions to send to GPT
 *           
 * *******************************************************************************************************************/

using System.ComponentModel;

namespace ChatGPTExtension
{
    public partial class ConfigurationWindow
    {
        #region Private Classes

        /// <summary>
        /// Represents a single action with a name and a prompt.
        /// </summary>
        public class ActionItem : INotifyPropertyChanged
        {
            private string _name;
            private string _prompt;

            public string Name
            {
                get { return _name; }
                set
                {
                    if (_name != value)
                    {
                        _name = value;
                        OnPropertyChanged("Name");
                    }
                }
            }

            public string Prompt
            {
                get { return _prompt; }
                set
                {
                    if (_prompt != value)
                    {
                        _prompt = value;
                        OnPropertyChanged("Prompt");
                    }
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion
    }
}
