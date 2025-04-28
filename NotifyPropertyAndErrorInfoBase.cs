
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using Newtonsoft.Json;

namespace UnifiVideoExporter
{
    public class NotifyPropertyAndErrorInfoBase : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private Guid Id;

        public NotifyPropertyAndErrorInfoBase()
        {
            Id = Guid.NewGuid();
            Errors = new Dictionary<string, IList<object>>();
            Children = new Dictionary<string, NotifyPropertyAndErrorInfoBase>();
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        #endregion

        #region INotifyDataErrorInfo

        protected void AddError(string propertyName, object newError)
        {
            if (!Errors.TryGetValue(propertyName, out IList<object>? propertyErrors))
            {
                propertyErrors = new List<object>();
                Errors.Add(propertyName, propertyErrors);
            }
            propertyErrors.Insert(0, newError);
            OnErrorsChanged(propertyName);
        }

        protected void AddErrorRange(string propertyName, IEnumerable<string> newErrors, bool isWarning = false)
        {
            if (!newErrors.Any())
            {
                return;
            }

            if (!Errors.TryGetValue(propertyName, out IList<object>? propertyErrors))
            {
                propertyErrors = new List<object>();
                Errors.Add(propertyName, propertyErrors);
            }

            if (isWarning)
            {
                foreach (string error in newErrors)
                {
                    propertyErrors.Add(error);
                }
            }
            else
            {
                foreach (string error in newErrors)
                {
                    propertyErrors.Insert(0, error);
                }
            }

            OnErrorsChanged(propertyName);
        }

        protected bool ClearErrors(string propertyName = "")
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                Errors.Clear();
                OnErrorsChanged(propertyName);
                return true;
            }
            if (Errors.Remove(propertyName))
            {
                OnErrorsChanged(propertyName);
                return true;
            }
            return false;
        }

        public bool PropertyHasErrors(string propertyName)
        {
            var errors = GetErrors(propertyName).Cast<string>().ToList();
            return errors != null && errors.Count > 0;
        }

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public System.Collections.IEnumerable GetErrors(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                //
                // We currently are not supporting "global" errors. All errors must be tied
                // to a property name in the form.
                //
                return new List<object>();
            }

            //
            // Note: propertyName is passed from our custom converter and includes the
            // full path to the property, which is necessary bc WPF binding strips the
            // parent path, leaving only the property name. We need the full path to
            // disambiguate parent properties from child properties of the same name.
            //

            //
            // Try this class first (parent)
            //
            if (Errors.TryGetValue(propertyName, out var errors))
            {
                return errors;
            }

            //
            // Fallback to child errors if no full path match.
            //
            var propName = propertyName;
            if (propName.Contains('.'))
            {
                var parts = propName.Split('.');
                if (parts.Length != 2)
                {
                    Debug.Assert(false);
                    return new List<object>();
                }
                var propertyNameInParent = parts[0];
                var propertyNameInChild = parts[1];
                if (!Children.TryGetValue(propertyNameInParent, out NotifyPropertyAndErrorInfoBase? child))
                {
                    Debug.Assert(false);
                    return new List<object>();
                }
                return child.GetErrors(propertyNameInChild);
            }
            return new List<object>();
        }

        [JsonIgnore]
        public bool HasErrors => Errors.Any();

        protected virtual void OnErrorsChanged(string propertyName)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        private Dictionary<string, IList<object>> Errors { get; }

        #endregion

        #region parent-child error propagation

        private Dictionary<string, NotifyPropertyAndErrorInfoBase> Children { get; set; }

        public void SubscribeToChildErrors(NotifyPropertyAndErrorInfoBase Child, string PropertyNameInParent)
        {
            Debug.Assert(!Children.ContainsKey(PropertyNameInParent));
            Children.Add(PropertyNameInParent, Child);
            Child.ErrorsChanged += OnChildErrorsChanged;
        }

        public void UnsubscribeFromChildErrors(NotifyPropertyAndErrorInfoBase Child)
        {
            var propertyNameInParent = GetChildProperyNameInParent(Child);
            Debug.Assert(!string.IsNullOrEmpty(propertyNameInParent));
            Child.ErrorsChanged -= OnChildErrorsChanged;
            Children.Remove(propertyNameInParent);
        }

        private void OnChildErrorsChanged(object? Sender, DataErrorsChangedEventArgs Args)
        {
            var child = Sender as NotifyPropertyAndErrorInfoBase;
            if (Sender == null || child == null)
            {
                Debug.Assert(false);
                return;
            }
            if (string.IsNullOrEmpty(Args.PropertyName))
            {
                return;
            }

            var nameInParent = GetChildProperyNameInParent(child);
            var propertyPath = $"{nameInParent}.{Args.PropertyName}";
            ClearErrors(propertyPath);
            if (child.PropertyHasErrors(Args.PropertyName))
            {
                var childErrors = child.GetErrors(Args.PropertyName).Cast<string>().ToList();
                AddErrorRange(propertyPath, childErrors!);
            }

            //
            // Notify the parent that an error has changed, because even though it doesn't
            // know about this child field directly, it's going to need to keep track of
            // the child's HasErrors property to see if the overall form is valid.
            //
            OnErrorsChanged(propertyPath);
        }

        private string? GetChildProperyNameInParent(NotifyPropertyAndErrorInfoBase Child)
        {
            var entry = Children.Where(c => c.Value == Child).FirstOrDefault();
            if (entry.Equals(default(KeyValuePair<string, NotifyPropertyAndErrorInfoBase>)) ||
                string.IsNullOrEmpty(entry.Key))
            {
                Debug.Assert(false);
                return null;
            }
            return entry.Key;
        }

        public NotifyPropertyAndErrorInfoBase? GetChild(string? PropertyName)
        {
            if (string.IsNullOrEmpty(PropertyName))
            {
                return null;
            }
            foreach (var kvp in Children)
            {
                if (PropertyName.StartsWith($"{kvp.Key}."))
                {
                    return kvp.Value;
                }
            }
            return null;
        }

        public void HandleChildSubscriptionForCollectionChangedNotification<T>(NotifyCollectionChangedEventArgs Args, string PropertyNameInParent = "")
        {
            if (Args.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (var item in Args.NewItems!)
                {
                    var newChild = item as NotifyPropertyAndErrorInfoBase;
                    if (newChild == null)
                    {
                        Debug.Assert(false);
                        return;
                    }
                    if (string.IsNullOrEmpty(PropertyNameInParent))
                    {
                        Debug.Assert(false);
                        return;
                    }
                    SubscribeToChildErrors(newChild, PropertyNameInParent);
                }
            }
            else if (Args.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (var item in Args.OldItems!)
                {
                    var child = item as NotifyPropertyAndErrorInfoBase;
                    if (child == null)
                    {
                        Debug.Assert(false);
                        return;
                    }
                    UnsubscribeFromChildErrors(child);
                }
            }
            else if (Args.Action == NotifyCollectionChangedAction.Replace)
            {
                foreach (var item in Args.NewItems!)
                {
                    var newChild = item as NotifyPropertyAndErrorInfoBase;
                    if (newChild == null)
                    {
                        Debug.Assert(false);
                        return;
                    }
                    if (string.IsNullOrEmpty(PropertyNameInParent))
                    {
                        Debug.Assert(false);
                        return;
                    }
                    SubscribeToChildErrors(newChild, PropertyNameInParent);
                }
                foreach (var item in Args.OldItems!)
                {
                    var child = item as NotifyPropertyAndErrorInfoBase;
                    if (child == null)
                    {
                        Debug.Assert(false);
                        return;
                    }
                    UnsubscribeFromChildErrors(child);
                }
            }
        }

        #endregion
    }
}
