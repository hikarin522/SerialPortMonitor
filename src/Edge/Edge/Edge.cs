using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;
using System.IO.Ports;
using System.Management;

namespace Edge
{
	public class Startup
	{
		public async Task<object> Invoke(object input)
		{
			return new Edge();
		}
	}

	public class Edge
	{
		public Edge()
		{
			mcWin32_SerialPort = new ManagementClass("Win32_SerialPort");
			mcWin32_PnPEntity = new ManagementClass("Win32_PnPEntity");
			serializer = new JavaScriptSerializer();
			GetPortInfo = _GetPortInfo;
		}

		public readonly Func<object, Task<object>> GetPortInfo;
		private readonly ManagementClass mcWin32_SerialPort;
		private readonly ManagementClass mcWin32_PnPEntity;
		private readonly JavaScriptSerializer serializer;

		private async Task<object> _GetPortInfo(object input)
		{
			var infoList = await Factory(mcWin32_SerialPort).Merge(Factory(mcWin32_PnPEntity))
				.Where(e => e != null).Where(e => e["Name"] != null)
				.Join(
					SerialPort.GetPortNames().ToObservable().Select(i => Tuple.Create(i, new Regex(i + @"[\p{P}\p{Z}$]"))),
					_ => Observable.Never<Unit>(),
					_ => Observable.Never<Unit>(),
					(l, r) => Tuple.Create(r.Item1, r.Item2, l)
				)
				.Where(obj => obj.Item2.IsMatch(obj.Item3["Name"].ToString()))
				.Select(obj => {
					var list = new Dictionary<string, object>();
					foreach (var prop in obj.Item3.Properties)
						list.Add(prop.Name, prop.Value);
					return Tuple.Create(obj.Item1,list);
				})
				.ToDictionary(i => i.Item1, i => i.Item2);

			return serializer.Serialize(infoList);;
		}

		private IObservable<ManagementBaseObject> Factory(ManagementClass mc)
		{
			var src = new ReplaySubject<ManagementBaseObject>();
			var ob = new ManagementOperationObserver();

			Observable.FromEventPattern<ObjectReadyEventHandler, ObjectReadyEventArgs>(
				h => h.Invoke,
				h => ob.ObjectReady += h,
				h => ob.ObjectReady -= h
			).Subscribe(obj => src.OnNext(obj.EventArgs.NewObject));

			Observable.FromEventPattern<CompletedEventHandler, CompletedEventArgs>(
				h => h.Invoke,
				h => ob.Completed += h,
				h => ob.Completed -= h
			).Subscribe(_ => src.OnCompleted());

			mc.GetInstances(ob);
			return src.AsObservable();
		}
	}
}
