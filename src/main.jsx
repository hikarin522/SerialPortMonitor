'use strict';

import app from 'app';
import BrowserWindow from 'browser-window';
import CrashReporter from 'crash-reporter';

export default class MainWindow {
	constructor() {
		CrashReporter.start();

		app.on('window-all-closed', () => {
			if (process.platform != 'darwin')
			app.quit();
		});

		app.on('ready', () => {
			var url = 'file://' + __dirname + '/index.html';
			this.createWindow(url, {width: 800, height: 600});
		});
	}

	createWindow(path, options) {
		this.window = new BrowserWindow(options);
		this.window.loadUrl(path);
		this.window.openDevTools();
		this.window.on('closed', () => {
			this.window = null;
		});
	}
};
