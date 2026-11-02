const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = name => fs.readFileSync(path.join(__dirname, '../../NEXT.web/wwwroot/js', name), 'utf8');

test('HTTP requests keep caller headers and include the Ajax contract', async () => {
    let actual;
    const window = {};
    vm.runInNewContext(source('http.js'), {window, Headers, fetch: async (url, options) => {actual = {url, options}; return 'response';}});
    assert.equal(await window.NextHttp.request('/save', {method: 'POST', headers: {'X-Test': 'value'}}), 'response');
    assert.equal(actual.options.credentials, 'same-origin');
    assert.equal(actual.options.method, 'POST');
    assert.equal(actual.options.headers.get('X-Test'), 'value');
    assert.equal(actual.options.headers.get('X-Requested-With'), 'XMLHttpRequest');
});

test('ProblemDetails retain localized guidance and validation descriptions', async () => {
    const window = {};
    vm.runInNewContext(source('http.js'), {window, Headers});
    const messages = {validation: 'Invalid', expired: 'Expired', forbidden: 'Forbidden', conflict: 'Conflict', error: 'Error'};
    assert.equal(await window.NextHttp.problemMessage({status: 422, json: async () => ({errors: {Name: ['Required'], Code: ['Duplicate']}})}, messages), 'Invalid\nRequired\nDuplicate');
    assert.equal(await window.NextHttp.problemMessage({status: 401, json: async () => {throw new Error();}}, messages), 'Expired');
    assert.equal(window.NextHttp.messageFor(403, messages), 'Forbidden');
    assert.equal(window.NextHttp.messageFor(409, messages), 'Conflict');
});

test('Submitting a form restores existing disabled states even after repeated busy calls', () => {
    const window = {};
    vm.runInNewContext(source('http.js'), {window, Headers});
    const buttons = [{disabled: false}, {disabled: true}];
    const attrs = new Map();
    const form = {querySelectorAll: () => buttons, setAttribute: (k,v) => attrs.set(k,v), removeAttribute: k => attrs.delete(k)};
    window.NextHttp.setBusy(form, true);
    window.NextHttp.setBusy(form, true);
    assert.ok(buttons.every(x => x.disabled));
    window.NextHttp.setBusy(form, false);
    assert.equal(buttons[0].disabled, false);
    assert.equal(buttons[1].disabled, true);
    assert.equal(attrs.has('aria-busy'), false);
});

test('Image picker validates the server-supplied limit before creating previews', () => {
    const events = new Map(), notices = [], revoked = [];
    let created = 0;
    const preview = {src: '', dataset: {placeholder: 'placeholder'}};
    const filename = {};
    const picker = {querySelector: selector => selector.includes('preview') ? preview : filename};
    const input = {value: 'image', files: [{name: 'valid.png', size: 10}], dataset: {maxBytes: '20'}, closest: () => picker};
    const document = {addEventListener: (name, fn) => events.set(name, fn), getElementById: () => ({dataset: {imageSize: 'Size error'}})};
    vm.runInNewContext(source('profile-image-picker.js'), {
        document, window: {addEventListener() {}, NextNotifications: {show: (...args) => notices.push(args)}},
        URL: {createObjectURL: () => 'preview-' + ++created, revokeObjectURL: url => revoked.push(url)}
    });
    const change = () => events.get('change')({target: {closest: () => input}});
    change();
    assert.equal(preview.src, 'preview-1');
    input.files = [{name: 'too-large.png', size: 21}];
    change();
    assert.equal(input.value, '');
    assert.equal(preview.src, 'placeholder');
    assert.equal(filename.textContent, '');
    assert.equal(created, 1);
    assert.deepEqual(revoked, ['preview-1']);
    assert.deepEqual(notices, [['Size error', 'error']]);
});
