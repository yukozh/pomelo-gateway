component.created = function () {
    this.loadEndpoints();
};

component.data = function () {
    return {
        endpoints: []
    };
};

component.methods = {
    loadEndpoints: async function () {
        this.endpoints = (await qv.get('/api/endpoint')).data;
    },
    close: function () {
        this.$parent.active = null;
        this.$parent.$container.close(this);
    },
};