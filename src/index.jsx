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
	async getPortInfo() {
		const info = await Rx.Observable.fromNodeCallback(this.dll.GetPortInfo)(null).toPromise();
		return JSON.parse(info);
	}
	async portInfoSource(ms) {
		const source = await Rx.Observable.fromNodeCallback(this.dll.PortInfoSource)(ms).toPromise();
		return Rx.Observable.create(async ob => {
			const dispose = await Rx.Observable.fromNodeCallback(source.subscribe)({
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
			return async () => await Rx.Observable.fromNodeCallback(dispose.dispose)(null).toPromise();
		}).map(e => JSON.parse(e));
	}
}();

var serialport;
async () => {
	const domReady = Rx.Observable.fromCallback(document.addEventListener)('DOMContentLoaded').toPromise();
	await Edge.init();
	serialport = await Edge.getPortInfo();
	console.log(serialport);
	source = await Edge.portInfoSource(2000);
	source.subscribe(x => console.log(x), e => console.log(e), () => console.log('onCompleted'));

	await domReady;
	Cycle.run(main, {
		DOM: makeDOMDriver('body')
	});
}();

function main({DOM}) {
	let actions = intent(DOM);
	let state$ = model(actions);
	return {
		DOM: view(state$)
	};
}

function intent(DOM) {
	return {
		toggleMenu$: DOM.select('#menu-toggle').events('click').map(e => true)
	};
}

function model(actions) {
	var toggle = true;
	return actions.toggleMenu$.map(e => toggle = !toggle).startWith(toggle);
}

function view(state$) {
	return state$.map(state =>
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
