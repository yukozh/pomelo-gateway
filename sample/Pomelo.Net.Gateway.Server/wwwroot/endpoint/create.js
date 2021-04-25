component.created = function () {
    this.loadTunnelAndRouters();
};

component.mounted = function () {
    this.$parent.active = 'create-endpoint';
};

component.data = function () {
    return {
        protocols: ['TCP', 'UDP'],
        streamRouters: [],
        streamTunnels: [],
        packetRouters: [],
        packetTunnels: [],
        validateFields: false,
        form: {
            name: '',
            protocol: 'TCP',
            address: '0.0.0.0',
            port: 0,
            routerId: null,
            tunnelId: null
        }
    };
};

component.watch = {
    deep: true,
    'form.protocol': function () {
        if (this.form.protocol == 'TCP' && !this.isRouterValid() && this.streamRouters.length) {
            this.form.routerId = this.streamRouters[0].id;
        }

        if (this.form.protocol == 'UDP' && !this.isRouterValid() && this.packetRouters.length) {
            this.form.routerId = this.packetRouters[0].id;
        }

        if (this.form.protocol == 'TCP' && !this.isTunnelValid() && this.streamTunnels.length) {
            this.form.tunnelId = this.streamTunnels[0].id;
        }

        if (this.form.protocol == 'UDP' && !this.isTunnelValid() && this.packetTunnels.length) {
            this.form.tunnelId = this.packetTunnels[0].id;
        }
    }
};

component.methods = {
    close: function () {
        this.$parent.active = null;
        this.$parent.$container.close(this);
    },
    create: async function () {
        var nId = app.notify('Creating Endpoint', `Validating arguments...`, 'blue', -1);
        this.validateFields = true;
        await sleep(500);
        var invalid = $('.invalid').length;
        console.log(invalid);
        if (invalid) {
            app.notify('Create Failed', `Arguments are invalid.`, 'red', 3, nId);
            return;
        }
        app.notify('Creating Endpoint', `Creating ${this.form.name}...`, 'blue', -1, nId);
        try {
            await qv.post('/api/endpoint', this.form);
            app.notify('Created Endpoint', `Created ${this.form.name}`, 'green', 5, nId);
            app.open('/endpoint');
        } catch (err) {
            app.notify('Create Failed', `Failed to create ${this.form.name}...`, 'red', 10, nId);
        }
    },
    isTunnelValid: function () {
        if (this.form.protocol == 'TCP') {
            return this.streamTunnels.some(x => this.form.tunnelId == x.id);
        } else {
            return this.packetTunnels.some(x => this.form.tunnelId == x.id);
        }
    },
    isRouterValid: function () {
        if (this.form.protocol == 'TCP') {
            return this.streamRouters.some(x => this.form.routerId == x.id);
        } else {
            return this.packetRouters.some(x => this.form.routerId == x.id);
        }
    },
    loadTunnelAndRouters: async function () {
        this.streamRouters = (await qv.get('/api/router/stream/providers')).data;
        if (this.form.protocol == 'TCP' && !this.isRouterValid() && this.streamRouters.length) {
            this.form.routerId = this.streamRouters[0].id;
        }
        this.streamTunnels = (await qv.get('/api/tunnel/stream/providers')).data;
        if (this.form.protocol == 'TCP' && !this.isTunnelValid() && this.streamTunnels.length) {
            this.form.tunnelId = this.streamTunnels[0].id;
        }
        this.packetRouters = (await qv.get('/api/router/packet/providers')).data;
        this.packetTunnels = (await qv.get('/api/tunnel/packet/providers')).data;
    }
};