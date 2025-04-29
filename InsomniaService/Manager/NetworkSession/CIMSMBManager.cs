using MadWizard.Insomnia.NetworkSession.Manager;
using MadWizard.Insomnia.Session.Manager;
using Microsoft.Management.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.NetworkSession.Manager
{
    internal abstract class CIMSMBManager<K, T>(string className) : IDisposable where K : notnull
    {
        private const string NAMESPACE = @"root\Microsoft\Windows\SMB";
        private const string DIALECT = "WQL";

        protected static TimeSpan MAX_OBJECT_AGE = TimeSpan.FromSeconds(5);

        private CimSession? _currentSession;

        private CimSession Session
        {
            get
            {
                if (_currentSession == null || !_currentSession.TestConnection())
                {
                    _currentSession?.Dispose();
                    _currentSession = null;

                    _currentSession = CimSession.Create(null);
                }

                return _currentSession;
            }
        }

        private readonly Dictionary<K, T> _objects = [];

        private DateTime _lastRefresh = DateTime.MinValue;

        public T this[K key] => Objects[key];

        protected IEnumerable<CimInstance> Instances
        {
            get
            {
                foreach (var instance in Session.QueryInstances(NAMESPACE, DIALECT, $"SELECT * FROM {className}"))
                    yield return instance;
            }
        }

        protected IDictionary<K, T> Objects
        {
            get
            {
                if (DateTime.Now - _lastRefresh > MAX_OBJECT_AGE)
                {
                    RemoveOrphans(RefreshObjects(_objects));

                    _lastRefresh = DateTime.Now;
                }

                return _objects;
            }
        }

        protected abstract ISet<K> RefreshObjects(Dictionary<K, T> objects);

        protected void RemoveOrphans(ISet<K> existingIDs)
        {
            foreach (var key in _objects.Keys.ToList())
            {
                if (!existingIDs.Contains(key))
                {
                    _objects.Remove(key);
                }
            }
        }

        public void Dispose()
        {
            _currentSession?.Dispose();
            _currentSession = null;
        }
    }
}
