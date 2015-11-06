'use strict';
/** @jsx hJSX */

var jQuery = require('jquery');
require('./node_modules/bootstrap/dist/js/bootstrap.min.js');

import Rx from 'rx';
import Cycle from '@cycle/core';
import {makeDOMDriver, h, hJSX} from '@cycle/dom';
const {div, span, nav, h1, h2, h3, h4, h5, h6, p, ul, li, a, button, code} = require('hyperscript-helpers')(h);

import remote from 'remote';
import path from 'path';
const edge = remote.require('electron-edge');

const Edge =  new class {
	async init() {
		const dllpath = path.join(__dirname, './Edge/Edge/bin/Release/Edge.dll');
		this.dll = await Rx.Observable.fromNodeCallback(edge.func(dllpath))(null).toPromise();
	}
	getPortInfo() {
		return Rx.Observable.fromNodeCallback(this.dll.GetPortInfo)(null)
			.map(x => JSON.parse(x));
	}
	portInfoSource(ms) {
		return Rx.Observable.fromNodeCallback(this.dll.PortInfoSource)(ms)
			.selectMany(source => Rx.Observable.create(ob => {
				const dispose = Rx.Observable.fromNodeCallback(source.subscribe)({
					onNext: (data, cb) => {
						ob.onNext(data);
						cb();
					},
					onError: (err, cb) => {
						ob.onError(err);
						cb();
					},
					onCompleted: (_, cb) => {
						ob.onCompleted();
						cb();
					}
				}).toPromise();
				return async () => await Rx.Observable.fromNodeCallback((await dispose).dispose)(null).toPromise();
			}))
			.map(x => JSON.parse(x))
			.do(x => console.log(x));
	}
	openSerialPort(name) {
		return Rx.Observable.fromNodeCallback(this.dll.OpenSerialPort)(name)
			.selectMany(source => Rx.Observable.create(ob => {
				const dispose = Rx.Observable.fromNodeCallback(source.subscribe)({
					onNext: (data, cb) => {
						ob.onNext(data);
						cb();
					},
					onError: (err, cb) => {
						ob.onError(err);
						cb();
					},
					onCompleted: (_, cb) => {
						ob.onCompleted();
						cb();
					}
				}).toPromise();
				return async () => await Rx.Observable.fromNodeCallback((await dispose).dispose)(null).toPromise();
			}));
			//.map(x => JSON.parse(x));
			//.do(x => console.log(x));
	}
}();

async () => {
	const domReady = Rx.Observable.fromCallback(document.addEventListener)('DOMContentLoaded').toPromise();
	await Edge.init();
	await domReady;

	Cycle.run(main, {
		DOM: makeDOMDriver('body')
	});

	Edge.openSerialPort('COM7').subscribe(x => console.log(x));
}();

function main({DOM}) {
	const source = Edge.portInfoSource(500);
	let actions = intent(DOM);
	let state$ = model(actions, source);
	return {
		DOM: view(state$)
	};
}

function intent(DOM) {
	return {
		toggleMenu$: DOM.select('#menu-toggle').events('click').map(e => true)
	};
}

function model(actions, serialport) {
	var toggle = true;
	return Rx.Observable.combineLatest(
		actions.toggleMenu$.map(e => toggle = !toggle).startWith(toggle),
		serialport,
		(state, serialport) => ({state, serialport})
	);
}

function view(state$) {
	return state$.map(({state, serialport}) =>
		div('#wrapper' + (state ? '' : '.toggled'), [
			div('#sidebar-wrapper', ul('.sidebar-nav', [].concat(
				li('.sidebar-brand', a({href: '#'}, 'Menu')),
				Object.keys(serialport).map(name => li(a({href: `#${name}`}, name)))
			))),
			div('#page-content-wrapper', div('.container-fluid', div('.row', div('.col-lg-12', [].concat(
				a('.btn.btn-default#menu-toggle', {href: '#'}, 'Toggle Menu'),
				Object.keys(serialport).map(name => div([].concat(
					h1(`#${name}`, name),
					Object.keys(serialport[name]).map(mc => div([].concat(
						h3(`${serialport[name][mc]['Name']} (${mc})`),
						ul(Object.keys(serialport[name][mc]).map(prop =>
							li(`${prop}: ${serialport[name][mc][prop]}`)
						))
					)))
				)))
			)))))
		])
	);
}
