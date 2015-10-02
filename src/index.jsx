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
		return (
			<div>
				<h3>{this.props.Name}</h3>
				<ul>
					<li>{this.props.DeviceID}</li>
					<li>{this.props.Caption}</li>
				</ul>
			</div>
		);
	}
}

class Ports extends React.Component {
	constructor(props) {
		super(props);
		this.state = {ports:[]};
	}
	componentDidMount() {
		setInterval(() => {
			cs.GetPorts(null, (err, res) => {
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
		var list = this.state.ports.map((port) => {
			return (<Port {...port}/>);
		});
		return (<div>{list}</div>);
	}
}

$(function() {
	React.render(<title>Electron!!</title>, document.head);
	React.render(<div><Hello /><Ports /></div>, document.body);
});

