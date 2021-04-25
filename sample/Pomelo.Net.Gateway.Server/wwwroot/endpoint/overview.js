component.data = function () {
    return {
        endpoint: null,
        streamRouters: [],
        streamTunnels: [],
        packetRouters: [],
        packetTunnels: [],
    };
};

component.mounted = function () {
    this.$parent.menu = 'overview';
    this.loadTunnelAndRouters();
};

component.computed = {
    tunnelName: function () {
        if (this.endpoint.protocol == 'TCP') {
            if (this.streamTunnels.filter(x => x.id == this.endpoint.tunnelId).length) {
                return this.streamTunnels.filter(x => x.id == this.endpoint.tunnelId)[0].name;
            } else {
                return '-';
            }
        } else {
            if (this.packetTunnels.filter(x => x.id == this.endpoint.tunnelId).length) {
                return this.packetTunnels.filter(x => x.id == this.endpoint.tunnelId)[0].name;
            } else {
                return '-';
            }
        }
    },
    routerName: function () {
        if (this.endpoint.protocol == 'TCP') {
            if (this.streamRouters.filter(x => x.id == this.endpoint.routerId).length) {
                return this.streamRouters.filter(x => x.id == this.endpoint.routerId)[0].name;
            } else {
                return '-';
            }
        } else {
            if (this.packetRouters.filter(x => x.id == this.endpoint.routerId).length) {
                return this.packetRouters.filter(x => x.id == this.endpoint.routerId)[0].name;
            } else {
                return '-';
            }
        }
    }
};

component.methods = {
    loadTunnelAndRouters: async function () {
        this.streamRouters = (await qv.get('/api/router/stream/providers')).data;
        this.streamTunnels = (await qv.get('/api/tunnel/stream/providers')).data;
        this.packetRouters = (await qv.get('/api/router/packet/providers')).data;
        this.packetTunnels = (await qv.get('/api/tunnel/packet/providers')).data;
    }
};