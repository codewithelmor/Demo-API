// httpyac settings that keep shared .http files portable between Visual Studio, VS Code, and the CI runner.
// 1. "# @expect 201" (or "# @expect 200 204") checks the status code. Visual Studio ignores # comments.
//    A wrong status is a failed test: httpyac exits with code 20 and lists it in --junit output.
// 2. Visual Studio request variables work in httpyac too:
//    {{createOrder.response.body.$.id}}, {{createOrder.response.body.*}}, {{createOrder.response.headers.Location}}.
const responses = new Map(); // "<file>|<request name>" -> { body, rawBody, headers }

function readJsonPath(value, path) {
  if (path === '$') return value;
  const parts = path.replace(/^\$\.?/, '').match(/[^.[\]]+|\[\d+\]/g) ?? [];
  return parts.reduce((current, part) => {
    if (current === undefined || current === null) return undefined;
    return part.startsWith('[') ? current[Number(part.slice(1, -1))] : current[part];
  }, value);
}

function lookup(fileName, name, part, selector) {
  const saved = responses.get(`${fileName}|${name}`);
  if (!saved) throw new Error(`Request "${name}" has not run yet. Run it first (it must come earlier in the file).`);
  if (part === 'headers') {
    const key = Object.keys(saved.headers).find(h => h.toLowerCase() === selector.toLowerCase());
    return key === undefined ? '' : String(saved.headers[key]);
  }
  if (selector === '*') return saved.rawBody ?? '';
  const result = readJsonPath(saved.body, selector);
  return result === undefined ? '' : typeof result === 'object' ? JSON.stringify(result) : String(result);
}

module.exports = {
  configureHooks(api) {
    api.hooks.onResponse.addHook('kitExpectStatus', (response, context) => {
      const expected = context.httpRegion.metaData.expect;
      if (!expected || expected === true) return;
      const allowed = String(expected).split(/[\s,|]+/).filter(Boolean);
      const message = `status ${response.statusCode} is ${allowed.join(' or ')}`;
      const passed = allowed.includes(String(response.statusCode));
      context.httpRegion.testResults ??= [];
      context.httpRegion.testResults.push(passed
        ? { message, status: 'SUCCESS' }
        : { message, status: 'FAILED', error: { error: new Error(message), displayMessage: `expected ${allowed.join(' or ')}, got ${response.statusCode}` } });
    });

    api.hooks.onResponse.addHook('kitSaveNamedResponse', (response, context) => {
      const name = context.httpRegion.metaData.name;
      if (!name || name === true) return;
      responses.set(`${context.httpFile.fileName}|${name}`, {
        body: response.parsedBody,
        rawBody: response.body,
        headers: response.headers ?? {},
      });
    });

    // Runs before httpyac evaluates {{...}} as JavaScript.
    api.hooks.replaceVariable.addHook('kitVisualStudioRequestVariables', (value, _type, context) => {
      if (typeof value !== 'string' || !value.includes('.response.')) return value;
      return value.replace(/\{\{\s*([\w-]+)\.response\.(body|headers)\.([^}\s]+)\s*\}\}/g,
        (_match, name, part, selector) => lookup(context.httpFile.fileName, name, part, selector));
    }, { before: ['xpath'] });
  },
};
