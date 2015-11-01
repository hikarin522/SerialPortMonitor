using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Edge {
	public class Startup {
		public async Task<object> Invoke(object input) {
			return new Edge();
		}
	}

	public class Edge {
		public Edge() {
			mcWin32_SerialPort = CreateMC("Win32_SerialPort");
			mcWin32_PnPEntity = CreateMC("Win32_PnPEntity");
			serializer = new JavaScriptSerializer();
			GetPortInfo = _GetPortInfo;
			PortInfoSource = _createSource;
		}

		public readonly Func<object, Task<object>> GetPortInfo;
		public readonly Func<object, Task<object>> PortInfoSource;
		private readonly Tuple<string, ManagementClass> mcWin32_SerialPort;
		private readonly Tuple<string, ManagementClass> mcWin32_PnPEntity;
		private readonly JavaScriptSerializer serializer;

		private async Task<object> _createSource(object input) {
			if (!(input is int) || (int)input <= 0) {
				return null;
			}
			var info = Observable.Interval(TimeSpan.FromMilliseconds((int)input))
				.SelectMany(_ => _GetPortInfo(null).ToObservable());

			Func<dynamic, Task<object>> Subscribe = async ob => {
				Func<object, Task<object>> onNext = null, onError = null, onCompleted = null;
				onNext = ob as Func<object, Task<object>>;
				if (onNext == null) {
					try { onNext = ob.onNext as Func<object, Task<object>>;	} catch { }
					try { onError = ob.onError as Func<object, Task<object>>; } catch { }
					try { onCompleted = ob.onCompleted as Func<object, Task<object>>; } catch { }
				}
				if (onNext == null) {
					return null;
				}
				var dispose = info.Subscribe(x => onNext(x), e => { if (onError != null) onError(e); }, () => { if (onCompleted != null) onCompleted(null); });
				Func<object, Task<object>> Dispose = async _ => { dispose.Dispose(); return null; };
				return new { dispose = Dispose };
			};

			return new { subscribe = Subscribe };
		}

		private Tuple<string, ManagementClass> CreateMC(string mc) {
			return Tuple.Create(mc, new ManagementClass(mc));
		}

		private async Task<object> _GetPortInfo(object input) {
			return await Factory(mcWin32_SerialPort).Merge(Factory(mcWin32_PnPEntity))
				.Where(e => e.Item2 != null).Where(e => e.Item2["Name"] != null)
				.Join(
					SerialPort.GetPortNames().ToObservable().Select(i => Tuple.Create(i, new Regex(i + @"[\p{P}\p{Z}$]"), new Dictionary<string, Dictionary<string, object>>())),
					_ => Observable.Never<Unit>(),
					_ => Observable.Never<Unit>(),
					(l, r) => Tuple.Create(l, r)
				)
				.Where(obj => obj.Item2.Item2.IsMatch(obj.Item1.Item2["Name"].ToString()))
				.Select(obj => {
					var list = new Dictionary<string, object>();
					foreach (var prop in obj.Item1.Item2.Properties) {
						list.Add(prop.Name, prop.Value);
					}
					obj.Item2.Item3.Add(obj.Item1.Item1, list);
					return Tuple.Create(obj.Item2.Item1, obj.Item2.Item3);
				})
				.Distinct(i => i.Item1)
				.ToDictionary(i => i.Item1, i => i.Item2)
				.Select(i => serializer.Serialize(i));
		}

		private IObservable<Tuple<string, ManagementBaseObject>> Factory(Tuple<string, ManagementClass> mc) {
			var src = new ReplaySubject<Tuple<string, ManagementBaseObject>>();
			var ob = new ManagementOperationObserver();

			Observable.FromEventPattern<ObjectReadyEventHandler, ObjectReadyEventArgs>(
				h => h.Invoke,
				h => ob.ObjectReady += h,
				h => ob.ObjectReady -= h
			).Subscribe(obj => src.OnNext(Tuple.Create(mc.Item1, obj.EventArgs.NewObject)));

			Observable.FromEventPattern<CompletedEventHandler, CompletedEventArgs>(
				h => h.Invoke,
				h => ob.Completed += h,
				h => ob.Completed -= h
			).Subscribe(_ => src.OnCompleted());

			mc.Item2.GetInstances(ob);
			return src.AsObservable();
		}
	}
}
