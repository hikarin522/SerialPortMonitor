'use strict';

var path = require('path');
var compiler = require('electron-compile');

compiler.initWithOptions({
	cacheDir: path.join(__dirname, './cache'),
	compilerOpts: {
		js: {stage: 0},
		jsx: {stage:0}
	}
});

var main = require(path.join(__dirname, './main'));
new main();
