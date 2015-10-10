#r "System.Management.dll"
#r "System.Web.Extensions.dll"
using System;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Management;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

public class Startup {
	public async Task<object> Invoke(object input) {
		return new Edge();
	}
}

public class Edge {
	public Func<object, Task<object>> GetPortNames = async (i) => {
		return SerialPort.GetPortNames();
	};

	public Func<object, Task<object>> GetPortInfo = async (i) => {
		var infoList = new Dictionary<string, Dictionary<string, object>>();

		var mcSerial = new ManagementClass("Win32_SerialPort");
		var win32_SerialPort = mcSerial.GetInstances();

		var mcPnP = new ManagementClass("Win32_PnPEntity");
		var win32_PnPEntity = mcPnP.GetInstances();

		var nameList = SerialPort.GetPortNames();
		foreach (string name in nameList) {
			string pattern = name + @"[\p{P}\p{Z}$]";
			var info = serchPort(win32_SerialPort, pattern);
			if (info == null) {
				info = serchPort(win32_PnPEntity, pattern);
			}
			if (info != null) {
				infoList.Add(name, info);
			}
		}

		var serializer = new JavaScriptSerializer();
		return serializer.Serialize(infoList);
	};

	static private Dictionary<string, object> serchPort(ManagementObjectCollection objCol, string pattern) {
		foreach (var obj in objCol) {
			if (Regex.IsMatch(obj.GetPropertyValue("Name").ToString(), pattern)) {
				var info = new Dictionary<string, object>();
				foreach (var prop in obj.Properties) {
					info.Add(prop.Name, prop.Value);
				}
				return info;
			}
		}
		return null;
	}
}
