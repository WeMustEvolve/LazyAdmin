using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ManagerADO
{
    class DynItem : INotifyPropertyChanged
    {
        private Dictionary<string, int> _propNames = new Dictionary<string, int>();
        private object[] _props;
        //private int _id = -1;
        //private string _name;
   
        public DynItem(Dictionary<string, int> properyNames)
        {
            // names: Id, Name, ... 

            _propNames = properyNames;
            _props = new object[_propNames.Count];
        }

        public DynItem(Dictionary<string, int> properyNames, object[] values)
        {
            // check _propNames.Count = values.length
            // names: Id, Name, ... 

            _propNames = properyNames;
            _props = values;
        }

        public int Id
        {
            get { return (int)_props[0]; }
            set 
            {
                if ((int)_props[0] != value)
                {
                    _props[0] = value;
                    //RaisePropertyChanged();
                }
            }
        }

        public string StateInfo {
            get { return _props[2] as string; }
            set
            {
                if ((string)_props[2] != value)
                {
                    _props[2] = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string Name
        {
            get { return (string)_props[1]; }
            set
            {
                if ((string)_props[1] != value)
                {
                    _props[1] = value;
                    RaisePropertyChanged();
                }
            }
        }

        public object this[string index]
        {
            get { 
                var intIndex = _propNames[index];
                return this[intIndex]; 
            }
            set {
                var intIndex = _propNames[index];
                if (_props[intIndex] != value)
                {
                    _props[intIndex] = value;
                    RaisePropertyChanged("Item[]");
                }
            }
        }

        public object this[int index]
        {
            get { return _props[index]; }
            set
            {
                _props[index] = value;
                RaisePropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string propName = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

    }
}
