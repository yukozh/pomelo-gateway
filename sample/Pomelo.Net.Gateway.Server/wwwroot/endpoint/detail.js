component.created = function () {
    this.$container = new PomeloComponentContainer('#detail', app, this, function (view) {
    }, function () { });

    this.loadEndpoint();
};

component.data = function () {
    return {
        id: null,
        endpoint: { name: null },
        menu: null
    };
};

component.methods = {
    loadEndpoint: async function () {
        this.endpoint = (await qv.get('/api/endpoint/' + this.id)).data;
        this.open('overview');
    },
    open: function(view) {
        this.menu = null;
        if (this.$container.active) {
            this.$container.close(this.$container.active);
        }
        this.$container.open('/endpoint/' + view, { endpoint: this.endpoint });
    },
    close: function () {
        this.$parent.active = null;
        this.$parent.$container.close(this);
    },
};