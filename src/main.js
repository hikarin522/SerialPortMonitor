'use strict';

var path = require('path');
var compiler = require('electron-compile');

compiler.initWithOptions({
	cacheDir: './cache',
	compilerOpts: {
		js: {stage: 0},
		jsx: {stage:0}
	}
});

var main = require('./main.jsx');
new main();
