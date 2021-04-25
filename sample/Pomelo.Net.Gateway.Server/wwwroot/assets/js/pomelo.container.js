String.prototype.replaceAll = function (s1, s2) {
    return this.replace(new RegExp(s1, "gm"), s2);
};

var PomeloComponentContainer = function (el, root, parent, onActive, onViewOpen) {
    return {
        el: el,
        instance: {},
        active: null,
        destroyAll: function () {
            for (var x in this.instance) {
                this.instance[x].$destroy();
            }
        },
        getTemplate: async function (url) {
            var result = await fetch(url + '.html');
            if (result.status === 404) {
                result = await fetch(url + '/index.html');
            }
            return result.text();
        },
        getScript: async function (url) {
            var result = await fetch(url + '.js');
            if (result.status === 404) {
                result = await fetch(url + '/index.js');
            }
            return result.text();
        },
        toSafeName: function (name) {
            return 'pomelo' + name.replaceAll('\/', '-').replaceAll('\\.', '-');
        },
        toQueryString: function (view, params) {
            var str = view + "?";
            for (var x in params) {
                str += `${x}=${params[x]}&`;
            }
            return str.substr(0, str.length - 1);
        },
        open: async function (view, params) {
            if (this.active) {
                this.active.$el.hidden = true;
                this.active = null;
            }

            var vm;
            if (view) {
                var qs = this.toQueryString(view, params);
                if (!this.instance[qs]) {
                    vm = await this.buildViewModel(view, params);
                    this.instance[qs] = vm;
                    var random = Math.ceil(Math.random() * 1000000000);
                    $(el).append('<div id="vm-' + random + '"></div>');
                    vm.$root = root;
                    vm.$parent = parent;
                    if (vm.reactive) {
                        var p = vm.reactive();
                        if (p instanceof Promise) {
                            await p;
                        }
                    }
                    vm.$mount('#vm-' + random);
                    vm.$identity = qs;
                    vm.$pomelo = this;
                } else {
                    if (this.instance[qs].reactive && typeof (this.instance[qs].reactive) === 'function') {
                        var p = this.instance[qs].reactive();
                        if (p instanceof Promise) {
                            await p;
                        }
                    }
                    this.instance[qs].$el.hidden = false;
                }

                if (this.instance[qs]) {
                    this.active = this.instance[qs];
                }
            }

            if (onViewOpen) {
                onViewOpen(view, params);
            }

            return vm;
        },
        reactive: async function (id) {
            if (this.instance[id].active) {
                var p = this.instance[qs].active();
                if (p instanceof Promise) {
                    await p;
                }
            }
            this.instance[qs].$el.hidden = false;
            if (this.instance[id]) {
                this.active = this.instance[id];
            }
        },
        close: async function (vm) {
            delete this.instance[vm.$identity];
            if (vm === this.active) {
                this.active = null;
            }
            if (vm.dispose && (typeof (vm.dispose) == 'function')) {
                var result = vm.dispose();
                if (result instanceof Promise) {
                    await result;
                }
            }
            vm.$el.remove();
            vm.$destroy();
        },
        closeById: function (id) {
            return this.close(this.instance[id]);
        },
        buildViewModel: async function (view, param) {
            var self = this;
            var html = await self.getTemplate(view);
            if (!html) {
                return null;
            }
            var js = await self.getScript(view);
            var component = { template: html };
            eval(js);
            this.addActiveFunction(component, view, onActive);
            self.addDataFunction(component, function (data) {
                if (!param) {
                    return;
                }
                for (var x in param) {
                    data[x] = param[x];
                }
            });
            var Com = Vue.component(self.toSafeName(view), component);
            var ret = new Com();
            ret.reactive = component.reactive;
            ret.dispose = component.dispose;
            return ret;
        },
        addCreatedFunction: function (component, view, func) {
            if (component.created === undefined) {
                component.created = function () { };
            }
            var origin = component.created;
            component.created = function () {
                origin.call(this);
                if (func) {
                    func.call(this, view, component);
                }
            };
        },
        addActiveFunction: function (component, view, func) {
            if (component.reactive === undefined) {
                component.reactive = function () { };
            }
            var origin = component.reactive;
            component.reactive = function () {
                origin.call(this);
                if (func) {
                    return func.call(this, view, component);
                }
            };
        },
        addDataFunction: function (component, func) {
            if (component.data === undefined) {
                component.data = function () { };
            }
            var origin = component.data;
            component.data = function () {
                var data = origin.call(this) || {};
                func.call(this, data);
                return data;
            };
        },
    };
};