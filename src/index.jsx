'use strict';

import prominence from "prominence";
import $ from 'jquery';

import React from 'react';
import {Jumbotron, PageHeader} from 'react-bootstrap';
import {Tabs, Tab} from 'react-bootstrap';

import remote from 'remote';
import path from 'path';

var edge = remote.require('electron-edge2');
var cs = edge.func(path.join(__dirname, './edge.cs'))(null, true);

class Title extends React.Component {
	render() {
		return (
			<Jumbotron><div className="container">
				<h1>SerialPortMonitor</h1>
				<p>Get the information of the serial port</p>
			</div></Jumbotron>
		);
	}
};

class Port extends React.Component {
	render() {
		var li = Object.keys(this.props.Info).map((name) => {
			return <li>{name}: {this.props.Info[name]}</li>;
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
	render() {
		var list = Object.keys(this.props).map((name) => {
			return (
				<Tab eventKey={name} title={this.props[name].Name}>
					<Port Name={name} Info={this.props[name]} />
				</Tab>
			);
		});
		return <Tabs position="left" animation={false}>{list}</Tabs>;
	}
}

class Body extends React.Component {
	constructor(props) {
		super(props);
		this.state = props;
	}
	componentDidMount() {
		setInterval(async () => {
			var res = await getPortInfo();
			console.log(res);
			this.setState({ports:res});
		}, 1000);
	}
	render() {
		return <div><Title /><Ports {...this.state.ports} /></div>;
	}
}

$(async () => {
	var res = await getPortInfo();
	React.render(<Body ports={res} />, document.body);
});

async function getPortInfo() {
	var res = await prominence(cs).GetPortInfo(null);
	var obj = JSON.parse(res);

	if (!Object.keys(obj).length) {
		obj = {"The serial port could not be detected": {
			"Name": "Undetectable"
		}};
	}

	return obj;
}
