#r "System.Management.dll"
using System;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Management;
using System.Collections;

public class Startup {
	public async Task<object> Invoke(object input) {
		return new Helper();
	}
}


public class Helper {
	public Func<object, Task<object>> GetSerialPorts = async (i) => {
		return SerialPort.GetPortNames();
	};

	public Func<object, Task<object>> GetPorts = async (i) => {
		var deviceNameList = new ArrayList();
		var mc = new ManagementClass("Win32_SerialPort");
		var manageObjCol = mc.GetInstances();
		foreach (var manageObj in manageObjCol) {
			var name = manageObj.GetPropertyValue("Name");
			if (name == null) {
				continue;
			}
			deviceNameList.Add(name.ToString());
		}
		return deviceNameList;
	};
}
