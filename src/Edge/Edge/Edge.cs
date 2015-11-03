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

namespace Edge {
	public class Startup {
		public async Task<object> Invoke(object input) {
			return new Edge();
		}
	}

	public class Edge {
		public Edge() {
			mcWin32_SerialPort = new ManagementClass("Win32_SerialPort");
			mcWin32_PnPEntity = new ManagementClass("Win32_PnPEntity");
			serializer = new JavaScriptSerializer();
			GetPortInfo = _GetPortInfo;
			PortInfoSource = _portInfoSource;
			OpenSerialPort = null;
		}

		public readonly Func<object, Task<object>> GetPortInfo;
		public readonly Func<object, Task<object>> PortInfoSource;
		public readonly Func<object, Task<object>> OpenSerialPort;
		private readonly ManagementClass mcWin32_SerialPort;
		private readonly ManagementClass mcWin32_PnPEntity;
		private readonly JavaScriptSerializer serializer;

		private async Task<object> _openSerialPort(dynamic input) {
			var portName = input as string;
			int baudRate = 9600, dataBits = 8;
			Parity parity = Parity.None;
			StopBits stopBits = StopBits.One;

			if (portName == null) {
				try { portName = input.portName as string; } catch { }
				try { baudRate = (input.baudRate is int) ? (int)input.baudRate : 9600; } catch { }
				try {
					switch (input.parity as string) {
					case "Even":
						parity = Parity.Even;
						break;
					case "Odd":
						parity = Parity.Odd;
						break;
					case "Mark":
						parity = Parity.Mark;
						break;
					case "Space":
						parity = Parity.Space;
						break;
					default:
						parity = Parity.None;
						break;
					}
				}
				catch { }
				try { dataBits = input.dataBits is int ? (int)input.dataBits : 8; } catch { }
				try {
					switch (input.stopBits as string) {
					case "None":
						stopBits = StopBits.None;
						break;
					case "OnePointFive":
						stopBits = StopBits.OnePointFive;
						break;
					case "Two":
						stopBits = StopBits.Two;
						break;
					default:
						stopBits = StopBits.One;
						break;
					}
				}
				catch { }
			}
			if (portName == null) {
				return null;
			}
			try {
				var serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits);
				serialPort.Open();


			}
			catch { }

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

		private IObservable<object> _createSource(int ms) {
			return Observable.Interval(TimeSpan.FromMilliseconds(ms)).StartWith(0)
				.Select(_ => SerialPort.GetPortNames())
				.Scan(Tuple.Create(new string[0], new string[0]), (Old, New) => Tuple.Create(Old.Item2, New))
				.Select(i => Tuple.Create(i.Item1.Except(i.Item2), i.Item2.Except(i.Item1)))
				.Where(i => i.Item1.Count() > 0 || i.Item2.Count() > 0)
				.SelectMany(async i => Tuple.Create(i.Item1, await i.Item2.ToObservable()
					.Select(j => Tuple.Create(j, new Regex(j + @"[\p{P}\p{Z}$]")))
					.Join(
						Factory(mcWin32_SerialPort).Merge(Factory(mcWin32_PnPEntity))
							.Select(j => Tuple.Create(j["Name"], j))
							.Where(j => j.Item1 != null),
						_ => Observable.Never<Unit>(),
						_ => Observable.Never<Unit>(),
						Tuple.Create
					)
					.Where(j => j.Item1.Item2.IsMatch(j.Item2.Item1.ToString()))
					.Select(j => {
						var list = new Dictionary<string, object>();
						foreach (var prop in j.Item2.Item2.Properties)
							list.Add(prop.Name, prop.Value);
						return Tuple.Create(j.Item1.Item1, j.Item2.Item2.ClassPath.ClassName, list);
					})
					.GroupBy(j => j.Item1)
					.SelectMany(async j => Tuple.Create(j.Key, await j.ToDictionary(k => k.Item2, k => k.Item3)))
					.ToArray()
				))
				.Select(i => i)
				.Scan(new Dictionary<string, object>(), (sum, New) => {
					foreach (var i in New.Item1)
						sum.Remove(i);
					foreach (var i in New.Item2)
						sum.Add(i.Item1, i.Item2);
					return sum;
				});
		}

		private IObservable<ManagementBaseObject> Factory(ManagementClass mc) {
			return Observable.Return(new ManagementOperationObserver())
				.SelectMany(
					ob => Observable.FromEventPattern<ObjectReadyEventHandler, ObjectReadyEventArgs>(
						h => { ob.ObjectReady += h; mc.GetInstances(ob); },
						h => ob.ObjectReady -= h
					)
					.TakeUntil(Observable.FromEventPattern<CompletedEventHandler, CompletedEventArgs>(
						h => ob.Completed += h,
						h => ob.Completed -= h
					))
				)
				.Select(e => e.EventArgs.NewObject);
		}
	}
}
