'use strict';

import $ from 'jquery';
import React from 'react';
import remote from 'remote';
import path from 'path';

var edge = remote.require('electron-edge2');
var cs = edge.func(path.join(__dirname, './edge.cs'))(null, true);
var comPorts;

class Hello extends React.Component {
	render() {
		return <div>Hello Electron & React!!!</div>;
	}
};

class Port extends React.Component {
	render() {
		var li = Object.keys(this.props.Info).map((name) => {
			return (<li>{name}: {this.props.Info[name]}</li>);
		});
		return (
			<div>
				<h3>{this.props.Name}</h3>
				<ul>{li}</ul>
			</div>
		);
	}
}

class Ports extends React.Component {
	constructor(props) {
		super(props);
		this.state = {ports:{}};
	}
	componentDidMount() {
		setInterval(() => {
			cs.GetPortInfo(null, (err, res) => {
				if (err) {
					console.log(err);
					return;
				}
				console.log(res);
				this.setState({ports:res});
			});
		}, 2000);
	}
	render() {
		var list = Object.keys(this.state.ports).map((name) => {
			return (<Port Name={name} Info={this.state.ports[name]} />);
		});
		return (<div>{list}</div>);
	}
}

$(function() {
	React.render(<title>Electron!!</title>, document.head);
	React.render(<div><Hello /><Ports /></div>, document.body);
});

