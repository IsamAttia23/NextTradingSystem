const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../../NEXT.web/wwwroot/js/filters.js'), 'utf8');

function setup(reduced = false) {
    const events = new Map(), windowEvents = new Map(), mediaEvents = new Map();
    const media = {matches: reduced, addEventListener: (name, handler) => mediaEvents.set(name, handler)};
    const document = {addEventListener: (name, handler) => events.set(name, handler)};
    const window = {
        matchMedia: () => media,
        getComputedStyle: body => ({opacity: body.opacity}),
        addEventListener: (name, handler) => windowEvents.set(name, handler)
    };
    vm.runInNewContext(source, {document, window});
    function createPanel(open = false) {
        const attributes = new Map(), animations = [];
        const pair = {height: null};
        const animate = (frames, options, height) => {
            const animation = {frames, options, cancelled: false, cancel() {
                this.cancelled = true;
                if (height) pair.height = null;
                else pair.body.opacity = '1';
            }};
            animations.push(animation);
            if (height) pair.height = Number.parseFloat(frames[0].height);
            else pair.body.opacity = frames[0].opacity;
            return animation;
        };
        pair.body = {opacity: '1', animate: (frames, options) => animate(frames, options, false)};
        pair.panel = {
            open, isConnected: true, matches: selector => selector === 'details[data-filter-panel]',
            querySelector: () => pair.body,
            getBoundingClientRect: () => ({height: pair.height ?? (pair.panel.open ? 300 : 40)}),
            animate: (frames, options) => animate(frames, options, true),
            setAttribute: (name, value) => attributes.set(name, value),
            removeAttribute: name => attributes.delete(name),
            toggleAttribute: (name, enabled) => enabled ? attributes.set(name, '') : attributes.delete(name)
        };
        pair.summary = {parentElement: pair.panel};
        pair.attributes = attributes;
        pair.animations = animations;
        return pair;
    }
    function click(pair, overrides = {}) {
        const event = {button: 0, defaultPrevented: false, target: {closest: () => pair.summary},
            preventDefault() {this.defaultPrevented = true;}, ...overrides};
        events.get('click')(event);
        return event;
    }
    return {createPanel, click, events, windowEvents, mediaEvents, media};
}

test('Filters animate height and opacity on both opening and closing', () => {
    const app = setup(), pair = app.createPanel();
    assert.ok(app.click(pair).defaultPrevented);
    assert.equal(pair.panel.open, true);
    assert.deepEqual(Array.from(pair.animations[0].frames, frame => frame.height), ['40px', '300px']);
    assert.deepEqual(Array.from(pair.animations[1].frames, frame => frame.opacity), ['0', '1']);
    assert.equal(pair.animations[0].options.duration, 220);
    pair.animations[0].onfinish();
    assert.equal(pair.panel.open, true);
    assert.equal(pair.attributes.size, 0);
    app.click(pair);
    assert.equal(pair.panel.open, true, 'Keep the body visible until closing finishes');
    assert.ok(pair.attributes.has('data-filter-closing'));
    assert.deepEqual(Array.from(pair.animations[2].frames, frame => frame.height), ['300px', '40px']);
    assert.deepEqual(Array.from(pair.animations[3].frames, frame => frame.opacity), ['1', '0']);
    pair.animations[2].onfinish();
    assert.equal(pair.panel.open, false);
    assert.equal(pair.attributes.size, 0);
    assert.ok(pair.animations.every(animation => animation.cancelled));
});

test('Rapid clicks reverse from the current height and ignore stale finish callbacks', () => {
    const app = setup(), pair = app.createPanel();
    app.click(pair);
    pair.height = 160;
    pair.body.opacity = '0.45';
    app.click(pair);
    assert.ok(pair.animations[0].cancelled);
    assert.deepEqual(Array.from(pair.animations[2].frames, frame => frame.height), ['160px', '40px']);
    assert.equal(pair.animations[3].frames[0].opacity, '0.45');
    pair.animations[0].onfinish();
    assert.ok(pair.attributes.has('data-filter-animating'));
    pair.height = 80;
    app.click(pair);
    assert.deepEqual(Array.from(pair.animations[4].frames, frame => frame.height), ['80px', '300px']);
    pair.animations[2].onfinish();
    assert.equal(pair.panel.open, true);
    pair.animations[4].onfinish();
    assert.equal(pair.attributes.size, 0);
    assert.equal(pair.panel.open, true);
});

test('Reduced motion and browsers without animations toggle immediately', () => {
    const app = setup(true), pair = app.createPanel();
    app.click(pair);
    assert.equal(pair.panel.open, true);
    app.click(pair);
    assert.equal(pair.panel.open, false);
    assert.equal(pair.animations.length, 0);
    app.media.matches = false;
    pair.body.animate = undefined;
    app.click(pair);
    assert.equal(pair.panel.open, true);
    assert.equal(pair.animations.length, 0);
});

test('Resize, preference changes and navigation clear active animations', () => {
    const app = setup(), pair = app.createPanel();
    app.click(pair);
    app.windowEvents.get('resize')();
    assert.equal(pair.panel.open, true);
    assert.equal(pair.attributes.size, 0);
    app.click(pair);
    app.media.matches = true;
    app.mediaEvents.get('change')();
    assert.equal(pair.panel.open, false);
    assert.equal(pair.attributes.size, 0);
    app.media.matches = false;
    app.click(pair);
    pair.panel.isConnected = false;
    app.events.get('next:navigated')();
    assert.ok(pair.animations.every(animation => animation.cancelled));
    const nextPage = app.createPanel();
    app.click(nextPage);
    assert.equal(nextPage.animations.length, 2, 'New page filters work without initialization');
    nextPage.animations[0].onfinish();
});

test('Other clicks and clicks prevented by another control keep their normal behavior', () => {
    const app = setup(), pair = app.createPanel();
    app.click(pair, {defaultPrevented: true});
    app.click(pair, {button: 2});
    const unrelated = app.click(pair, {target: {closest: () => null}});
    assert.equal(unrelated.defaultPrevented, false);
    assert.equal(pair.panel.open, false);
    assert.equal(pair.animations.length, 0);
});
