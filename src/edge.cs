using System;
using System.Threading.Tasks;
using System.IO.Ports;

public class Startup {
	public async Task<object> Invoke(object input) {
		return new Helper();
	}
}

public class Helper {
	public Func<object, Task<object>> GetSerialPorts = _GetSerialPorts;
	private static async Task<object> _GetSerialPorts(object input) {
		return SerialPort.GetPortNames();
	}
}
