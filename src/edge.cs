#r "System.Management.dll"
using System;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Management;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class Startup {
	public async Task<object> Invoke(object input) {
		return new Helper();
	}
}

public class Helper {
	public Func<object, Task<object>> GetPortNames = async (i) => {
		return SerialPort.GetPortNames();
	};

	public Func<object, Task<object>> GetPortInfo = async (i) => {
		var infoList = new Dictionary<string, Dictionary<string, object>>();
		var mc = new ManagementClass("Win32_PnPEntity");
		var mciList = mc.GetInstances();
		var nameList = SerialPort.GetPortNames();

		foreach (string name in nameList) {
			string pattern = name + @"[\p{P}\p{Z}$]";
			foreach (var mci in mciList) {
				if (Regex.IsMatch(mci.GetPropertyValue("Name").ToString(), pattern)) {
					var info = new Dictionary<string, object>();
					foreach (var prop in mci.Properties) {
						info.Add(prop.Name, prop.Value);
					}
					infoList.Add(name, info);
					break;
				}
			}
		}
		return infoList;
	};
}
