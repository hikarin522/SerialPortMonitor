using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Text;

namespace Edge {
	public class Startup {
		public async Task<object> Invoke(object input) {
			return new Edge();
		}
	}

	public class Edge : IDisposable {
		private readonly ObservableManagementClass mcWin32_SerialPort;
		private readonly ObservableManagementClass mcWin32_PnPEntity;
		private readonly JavaScriptSerializer serializer;

		public readonly Func<object, Task<object>> GetPortInfo;
		public readonly Func<dynamic, Task<object>> PortInfoSource;
		public readonly Func<dynamic, Task<object>> OpenSerialPort;

		public Edge() {
			mcWin32_SerialPort = new ObservableManagementClass("Win32_SerialPort");
			mcWin32_PnPEntity = new ObservableManagementClass("Win32_PnPEntity");
			serializer = new JavaScriptSerializer();
			GetPortInfo = _GetPortInfo;
			PortInfoSource = _portInfoSource;
			OpenSerialPort = _openSerialPort;
		}

		public void Dispose() {
			mcWin32_PnPEntity.Dispose();
			mcWin32_SerialPort.Dispose();
		}

		private async Task<object> _openSerialPort(dynamic input) {
			string portName = input as string;
			int baudRate = 9600, dataBits = 8;
			Parity parity = Parity.None;
			StopBits stopBits = StopBits.One;

			if (portName == null) {
				try { portName = input.portName as string; } catch { }
				try { baudRate = (input.baudRate is int) ? (int)input.baudRate : 9600; } catch { }
				try { switch (input.parity as string) {
					case "Even": parity = Parity.Even; break;
					case "Odd":	parity = Parity.Odd; break;
					case "Mark": parity = Parity.Mark; break;
					case "Space": parity = Parity.Space; break;
					default: parity = Parity.None; break;
				}} catch { }
				try { dataBits = input.dataBits is int ? (int)input.dataBits : 8; } catch { }
				try { switch (input.stopBits as string) {
					case "None": stopBits = StopBits.None; break;
					case "OnePointFive": stopBits = StopBits.OnePointFive; break;
					case "Two": stopBits = StopBits.Two; break;
					default: stopBits = StopBits.One; break;
				}} catch { }
			}

			if (portName == null)
				return null;

			try {
                var serialPort = new ObservableSerialPort(new SerialPort(portName, baudRate, parity, dataBits, stopBits));
				var source = serialPort.Publish().RefCount();
				return new {
					subscribe = (Func<dynamic, Task<object>>)(async ob => {
						Func<object, Task<object>> onNext, onError = null, onCompleted = null;
						onNext = ob as Func<object, Task<object>>;
						try { onNext = ob.onNext as Func<object, Task<object>>; } catch { }
						try { onError = ob.onError as Func<object, Task<object>>; } catch { }
						try { onCompleted = ob.onCompleted as Func<object, Task<object>>; } catch { }
						if (ob == null)
							return null;

						var dispose = source.Subscribe(
							i => onNext(i),
							e => { if (onError != null) onError(e); },
							() => { if (onCompleted != null) onCompleted(null); }
						);

						return new {
							dispose = (Func<object, Task<object>>)(async _ => {
								dispose.Dispose();
								return null;
							})
						};
					})
				};
			} catch { }

			return null;
		}

		private async Task<object> _GetPortInfo(object _) {
			return await _createSource(0).Select(i => serializer.Serialize(i)).FirstAsync();
		}

		private async Task<object> _portInfoSource(object input) {
			if (!(input is int) || (int)input <= 0)
				return null;

			var info = _createSource((int)input)
				.Select(i => serializer.Serialize(i))
				.Publish().RefCount();

			return new {
				subscribe = (Func<dynamic, Task<object>>)(async ob => {
					Func<object, Task<object>> onNext, onError = null, onCompleted = null;
					onNext = ob as Func<object, Task<object>>;
					if (onNext == null) {
						try { onNext = ob.onNext as Func<object, Task<object>>; } catch { }
						try { onError = ob.onError as Func<object, Task<object>>; } catch { }
						try { onCompleted = ob.onCompleted as Func<object, Task<object>>; } catch { }
					}
					if (onNext == null)
						return null;

					var dispose = info.Subscribe(
						i => onNext(i),
						e => { if (onError != null) onError(e); },
						() => { if (onCompleted != null) onCompleted(null); }
					);

					return new {
						dispose = (Func<object, Task<object>>)(async _ => {
							dispose.Dispose();
							return null;
						})
					};
				})
			};
		}

		private IObservable<IDictionary<string, IDictionary<string, Dictionary<string, string>>>> _createSource(int ms) {
			return Observable.Interval(TimeSpan.FromMilliseconds(ms)).StartWith(0)
				.Select(_ => SerialPort.GetPortNames())
				.Scan(Tuple.Create(new string[0], new string[0]), (Old, New) => Tuple.Create(Old.Item2, New))
				.Select(i => Tuple.Create(i.Item1.Except(i.Item2), i.Item2.Except(i.Item1)))
				.Where(i => i.Item1.Count() > 0 || i.Item2.Count() > 0)
				.Select(async i => Tuple.Create(i.Item1, await i.Item2.ToObservable()
					.Select(j => Tuple.Create(j, new Regex(j + @"[\p{P}\p{Z}$]")))
					.Join(
						mcWin32_SerialPort.Merge(mcWin32_PnPEntity)
							.Select(j => Tuple.Create(j.ClassPath.ClassName, j["Name"] as string, j.Properties))
							.Where(j => j.Item2 != null),
						_ => Observable.Never<Unit>(),
						_ => Observable.Never<Unit>(),
						Tuple.Create
					)
					.Where(j => j.Item1.Item2.IsMatch(j.Item2.Item2))
					.Select(j => {
						var list = new Dictionary<string, string>();
						foreach (var prop in j.Item2.Item3)
							list.Add(prop.Name, prop.Value as string);
						return Tuple.Create(j.Item1.Item1, j.Item2.Item1, list);
					})
					.GroupBy(j => j.Item1)
					.SelectMany(async j => Tuple.Create(j.Key, await j.ToDictionary(k => k.Item2, k => k.Item3)))
					.ToArray()
				))
				.Concat()
				.Scan(new Dictionary<string, IDictionary<string, Dictionary<string, string>>>(), (sum, New) => {
					foreach (var i in New.Item1)
						sum.Remove(i);
					foreach (var i in New.Item2)
						sum.Add(i.Item1, i.Item2);
					return sum;
				});
		}
	}

	class ObservableManagementClass : IObservable<ManagementBaseObject>, IDisposable {
		private readonly ManagementClass _mc;
		private readonly ManagementOperationObserver _observer;
		private readonly IObservable<ManagementBaseObject> _observable;
		private ReplaySubject<ManagementBaseObject> _subject = null;
		private bool _isSubscribe = false;
		public string ClassName { get { return _mc.ClassPath.ClassName; } }

		public ObservableManagementClass(string className) {
			_mc = new ManagementClass(className);
			_observer = new ManagementOperationObserver();
			_observable = Observable.FromEventPattern<ObjectReadyEventHandler, ObjectReadyEventArgs>(
					h => _observer.ObjectReady += h, h => _observer.ObjectReady -= h
				).TakeUntil(Observable.FromEventPattern<CompletedEventHandler, CompletedEventArgs>(
					h => _observer.Completed += h, h => _observer.Completed -= h
				)).Select(e => e.EventArgs.NewObject)
				.Finally(() => _isSubscribe = false);
		}

		public void Dispose() {
			_observer.Cancel();
			_mc.Dispose();
		}

		public IDisposable Subscribe(IObserver<ManagementBaseObject> observer) {
			if (!_isSubscribe) {
				_isSubscribe = true;
				_subject = new ReplaySubject<ManagementBaseObject>();
				_observable.Subscribe(
					i => _subject.OnNext(i),
					e => _subject.OnError(e),
					() => _subject.OnCompleted()
				);
				_mc.GetInstances(_observer);
			}
			return _subject.Subscribe(observer);
		}
	}

	class ObservableSerialPort : IObservable<string>, IDisposable {
		private readonly SerialPort _serialPort;

		public ObservableSerialPort(SerialPort serialPort) {
			_serialPort = serialPort;
			_serialPort.Open();
		}

		public void Dispose() {
			_serialPort.Dispose();
		}

		public IDisposable Subscribe(IObserver<string> observer) {
			var rcvEvent = Observable.FromEventPattern<SerialDataReceivedEventHandler, SerialDataReceivedEventArgs>(
					h => _serialPort.DataReceived += h, h => _serialPort.DataReceived -= h
				).TakeWhile(e => e.EventArgs.EventType != SerialData.Eof)
				.Select(e => e.Sender).Cast<SerialPort>()
				.Select(e => {
					var buf = new byte[e.BytesToRead];
					e.Read(buf, 0, buf.Length);
					return Encoding.ASCII.GetString(buf);
				}).Scan(new string[1]{""}, (Sum, New) => string.Concat(Sum.Last(), New).Split('\n'))
				.SelectMany(i => i.Take(i.Length - 1))
				.Subscribe(observer);

			var errEvent = Observable.FromEventPattern<SerialErrorReceivedEventHandler, SerialErrorReceivedEventArgs>(
					h => _serialPort.ErrorReceived += h, h => _serialPort.ErrorReceived -= h
				).Subscribe(e => observer.OnError(new Exception(e.EventArgs.EventType.ToString())));

			return Disposable.Create(() => {
				rcvEvent.Dispose();
				errEvent.Dispose();
			});
		}

		public void Send(string text) {
			_serialPort.Write(text);
		}
	}
}
