'use strict';

var path = require('path');
require('electron-compile').init();

var main = require(path.join(__dirname, './main'));
new main();
