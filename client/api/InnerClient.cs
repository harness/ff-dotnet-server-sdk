using io.harness.cfsdk.client.api.analytics;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.client.polling;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace io.harness.cfsdk.client.api
{
    internal class InnerClient :
        IAuthCallback,
        IRepositoryCallback,
        IPollCallback,
        IUpdateCallback,
        IEvaluatorCallback,
        IMetricCallback,
        IConnectionCallback
    {

        readonly ConcurrentDictionary<IObserver<Event>, HashSet<NotificationType>> observers = new ConcurrentDictionary<IObserver<Event>, HashSet<NotificationType>>();

        // Services
        private IAuthService authService;
        private IRepository repository;
        private IPollingProcessor polling;
        private IUpdateProcessor update;
        private IEvaluator evaluator;
        private IMetricsProcessor metric;
        private IConnector connector;


        public InnerClient(string apiKey, Config config)
        {
            Initialize(apiKey, config);
        }

        public InnerClient(IConnector connector, Config config)
        {
            Initialize(connector, config);
        }

        public void Initialize(string apiKey, Config config)
        {
            Initialize(new HarnessConnector(apiKey, config, this), config);
        }

        public void Initialize(IConnector connector, Config config)
        {
            this.connector = connector;
            this.authService = new AuthService(connector, config, this);
            this.repository = new StorageRepository(config.Cache, config.Store, this);
            this.polling = new PollingProcessor(connector, this.repository, config, this);
            this.update = new UpdateProcessor(connector, this.repository, config, this);
            this.evaluator = new Evaluator(this.repository, this);
            this.metric = new MetricsProcessor(connector, config, this);
        }
        public async Task StartAsync()
        {
            Log.Information("Initialize authentication");
            // Start Authentication flow  
            this.authService.Start();

            var initWork = new[] {
                this.polling.ReadyAsync()
            };

            // We finished with initialization when Polling processor returns.
            await Task.WhenAll(initWork);

            Notify(new Event { type = NotificationType.READY });
        }
        #region Stream callback

        public void OnStreamConnected()
        {
            this.polling.Stop();
        }
        public void OnStreamDisconnected()
        {
            this.polling.Start();
        }
        #endregion



        #region Authentication callback
        public void OnAuthenticationSuccess()
        {
            // after successfull authentication, start 
            polling.Start();
            update.Start();
            metric.Start();
        }

        #endregion

        #region Reauthentication callback
        public void OnReauthenticateRequested()
        {
            polling.Stop();
            update.Stop();
            metric.Stop();

            authService.Start();
        }
        #endregion

        #region Poller Callback
        public void OnPollerReady()
        {

        }
        public void OnPollError(string message)
        {

        }
        #endregion

        #region Repository callback

        public void OnFlagStored(string identifier)
        {
            Notify(new Event { identifier = identifier , type = NotificationType.CHANGED });
        }

        public void OnFlagDeleted(string identifier)
        {
            Notify(new Event { identifier = identifier, type = NotificationType.CHANGED });
        }

        public void OnSegmentStored(string identifier)
        {
            repository.FindFlagsBySegment(identifier).ToList().ForEach(i => {
               Notify(new Event { identifier = i, type = NotificationType.CHANGED });

            });
        }

        public void OnSegmentDeleted(string identifier)
        {
            repository.FindFlagsBySegment(identifier).ToList().ForEach(i => {
                Notify(new Event { identifier = i, type = NotificationType.CHANGED });

            });
        }
        #endregion

        public bool BoolVariation(string key, dto.Target target, bool defaultValue)
        {
            return evaluator.BoolVariation(key, target, defaultValue);
        }
        public string StringVariation(string key, dto.Target target, string defaultValue)
        {
            return evaluator.StringVariation(key, target, defaultValue);
        }
        public double NumberVariation(string key, dto.Target target, double defaultValue)
        {
            return evaluator.NumberVariation(key, target, defaultValue);
        }
        public JObject JsonVariation(string key, dto.Target target, JObject defaultValue)
        {
            return evaluator.JsonVariation(key, target, defaultValue);
        }

        public void Close()
        {
            this.connector.Close();
            this.observers.Clear();
            this.authService.Stop();
            this.repository.Close();
            this.polling.Stop();
            this.update.Stop();
            this.metric.Stop();
        }

        #region Notification managegement

        private void Notify(Event e)
        {
            foreach(IObserver<Event> ob in observers.Keys)
            {
                if (observers.TryGetValue(ob, out HashSet<NotificationType> set))
                {
                    if (set != null && (set.Contains(e.type) || set.Contains(NotificationType.ALL)))
                    {
                        ob.OnNext(e);
                    }
                }
            }
        }

        public IDisposable Subscribe(IObserver<Event> observer)
        {
            return Subscribe(NotificationType.ALL, observer);
        }

        public IDisposable Subscribe(NotificationType evn, IObserver<Event> observer)
        {
            HashSet<NotificationType> set = observers.GetOrAdd(observer, new HashSet<NotificationType>());
            set.Add(evn);

            return new Unsubscriber(observers, observer, evn);
        }

        public void Update(Message message, bool manual)
        {
            this.update.Update(message, manual);
        }

        public void evaluationProcessed(FeatureConfig featureConfig, dto.Target target, Variation variation)
        {
            this.metric.PushToQueue(target, featureConfig, variation);
        }


        private class Unsubscriber : IDisposable
        {
            private ConcurrentDictionary<IObserver<Event>, HashSet<NotificationType>> _observers;
            private IObserver<Event> _observer;
            private NotificationType _evn;

            public Unsubscriber(ConcurrentDictionary<IObserver<Event>, HashSet<NotificationType>> observers, IObserver<Event> observer, NotificationType evn)
            {
                this._observers = observers;
                this._observer = observer;
                this._evn = evn;
            }

            public void Dispose()
            {
                HashSet<NotificationType> notifications;
                if( _observers.TryGetValue(this._observer, out notifications) )
                {
                    notifications.Remove(this._evn);
                    if(notifications.Count == 0)
                    {

                        _observers.TryRemove(this._observer, out notifications);
                    }
                }
            }
        }
        #endregion
    }

}
