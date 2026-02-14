// ═══════════════════════════════════════════════════════════════════════
// JsArray DSL Transpiler Engine
// Maps C# phantom-type expressions → JavaScript array method calls
// Shalimar Foundation DSL · Determinism Target: 100%
// ═══════════════════════════════════════════════════════════════════════

(function(exports) {
    'use strict';

    // ── AST Node Types ──────────────────────────────────────────────────
    // The transpiler works on a simplified AST that mirrors C# expression trees.
    // Each node type maps to a visitor method.

    var NodeType = {
        Identifier:   'Identifier',    // variable reference
        Member:       'Member',        // obj.prop
        Index:        'Index',         // obj[idx]
        Call:         'Call',          // obj.method(args)
        Lambda:       'Lambda',        // (params) => body
        Literal:      'Literal',       // 42, "hello", true
        Binary:       'Binary',        // a + b, a === b
        Unary:        'Unary',         // !a
        Conditional:  'Conditional',   // a ? b : c
        Assign:       'Assign',        // a = b
        Statement:    'Statement',     // expression statement
        Block:        'Block',         // { stmts }
        VarDecl:      'VarDecl',       // var x = expr
        IfElse:       'IfElse',        // if/else
    };

    // ── C# → JS Method Name Map (from [JsEmit] attributes) ─────────────
    var JS_EMIT_MAP = {
        // Iteration (lambda)
        'Filter':    'filter',
        'Map':       'map',
        'Find':      'find',
        'FindIndex': 'findIndex',
        'Some':      'some',
        'Every':     'every',
        'ForEach':   'forEach',
        'Reduce':    'reduce',
        // Lookup (no lambda)
        'IndexOf':   'indexOf',
        'Includes':  'includes',
        'Join':      'join',
        // Slicing & combining
        'Slice':     'slice',
        'Concat':    'concat',
        'Reverse':   'reverse',
        // Sorting
        'Sort':      'sort',
        // Mutation
        'Push':      'push',
        'Pop':       'pop',
        'Shift':     'shift',
        'Unshift':   'unshift',
        'Splice':    'splice',
    };

    // Methods whose lambda is Action<T> (void — no return keyword)
    var VOID_LAMBDA_METHODS = { 'ForEach': true, 'forEach': true };

    // Methods that take lambda arguments
    var LAMBDA_METHODS = {
        'Filter': true, 'Map': true, 'Find': true, 'FindIndex': true,
        'Some': true, 'Every': true, 'ForEach': true, 'Reduce': true,
        'Sort': true,
    };

    // ── C# → JS Property Name Map ──────────────────────────────────────
    var JS_PROP_MAP = {
        'Length':    'length',
        'IsActive': 'isActive',
        'Name':     'name',
        'Id':       'id',
        'Amount':   'amount',
        'FacilityId': 'facilityId',
        'InnerText': 'innerText',
        'Disabled':  'disabled',
        'DataSource': 'dataSource',
        'Data':      'data',
        'RowIndex':  'rowIndex',
    };

    // ── C# → JS Method Map for non-array types ─────────────────────────
    var JS_METHOD_MAP = {
        'SelectRow':           'selectRow',
        'SelectedRecords':     'getSelectedRecords',
        'SelectedRowIndexes':  'getSelectedRowIndexes',
        'Log':                 'log',
        'LocaleCompare':       'localeCompare',
    };

    // ── C# → JS Identifier Map ─────────────────────────────────────────
    var JS_IDENT_MAP = {
        'Grid':        'residentGrid',
        'StatusLabel': 'statusLabel',
        'SubmitBtn':   'submitBtn',
        'JsConsole':   'console',
    };

    // ── AST Builder Helpers ─────────────────────────────────────────────
    function id(name)       { return { type: NodeType.Identifier, name: name }; }
    function lit(value)     { return { type: NodeType.Literal, value: value }; }
    function member(obj, prop) { return { type: NodeType.Member, object: obj, property: prop }; }
    function index(obj, idx)   { return { type: NodeType.Index, object: obj, index: idx }; }
    function call(obj, method, args) {
        return { type: NodeType.Call, object: obj, method: method, arguments: args || [] };
    }
    function lambda(params, body, isVoid) {
        return { type: NodeType.Lambda, params: params, body: body, isVoid: !!isVoid };
    }
    function binary(op, left, right) {
        return { type: NodeType.Binary, operator: op, left: left, right: right };
    }
    function unary(op, operand) { return { type: NodeType.Unary, operator: op, operand: operand }; }
    function assign(target, value) { return { type: NodeType.Assign, target: target, value: value }; }
    function varDecl(name, init)   { return { type: NodeType.VarDecl, name: name, init: init }; }
    function stmt(expr)            { return { type: NodeType.Statement, expression: expr }; }
    function ifElse(cond, then, els) { return { type: NodeType.IfElse, condition: cond, then: then, else: els }; }
    function block(stmts) { return { type: NodeType.Block, statements: stmts }; }

    // ── C# Operator → JS Operator ───────────────────────────────────────
    var OP_MAP = {
        '==': '===',
        '!=': '!==',
        '+':  '+',
        '-':  '-',
        '*':  '*',
        '/':  '/',
        '%':  '%',
        '&&': '&&',
        '||': '||',
        '>':  '>',
        '<':  '<',
        '>=': '>=',
        '<=': '<=',
    };

    // ── Expression Visitor ──────────────────────────────────────────────
    function visit(node) {
        if (!node) return '';
        switch (node.type) {
            case NodeType.Identifier:
                return JS_IDENT_MAP[node.name] || node.name;

            case NodeType.Literal:
                if (typeof node.value === 'string') return "'" + node.value + "'";
                return String(node.value);

            case NodeType.Member: {
                var obj = visit(node.object);
                var prop = node.property;
                // Check if it's a method-as-property (like SelectedRecords)
                if (JS_METHOD_MAP[prop]) {
                    return obj + '.' + JS_METHOD_MAP[prop] + '()';
                }
                var jsProp = JS_PROP_MAP[prop] || prop;
                return obj + '.' + jsProp;
            }

            case NodeType.Index: {
                var obj = visit(node.object);
                var idx = visit(node.index);
                return obj + '[' + idx + ']';
            }

            case NodeType.Call: {
                var obj = visit(node.object);
                var method = node.method;
                var jsMethod = JS_EMIT_MAP[method] || JS_METHOD_MAP[method] || method;

                var args = [];
                for (var i = 0; i < node.arguments.length; i++) {
                    var arg = node.arguments[i];
                    if (arg.type === NodeType.Lambda) {
                        var isVoidMethod = !!VOID_LAMBDA_METHODS[method];
                        args.push(visitLambda(arg, isVoidMethod));
                    } else {
                        args.push(visit(arg));
                    }
                }
                return obj + '.' + jsMethod + '(' + args.join(', ') + ')';
            }

            case NodeType.Lambda:
                return visitLambda(node, node.isVoid);

            case NodeType.Binary: {
                var left = visit(node.left);
                var right = visit(node.right);
                var op = OP_MAP[node.operator] || node.operator;
                return left + ' ' + op + ' ' + right;
            }

            case NodeType.Unary:
                return node.operator + visit(node.operand);

            case NodeType.Assign: {
                var target = visit(node.target);
                var value = visit(node.value);
                return target + ' = ' + value;
            }

            case NodeType.VarDecl: {
                var init = visit(node.init);
                return 'var ' + node.name + ' = ' + init + ';';
            }

            case NodeType.Statement:
                return visit(node.expression) + ';';

            case NodeType.IfElse: {
                var cond = visit(node.condition);
                var out = 'if (' + cond + ') {\n';
                out += indent(visitBlock(node.then)) + '\n}';
                if (node.else) {
                    out += ' else {\n' + indent(visitBlock(node.else)) + '\n}';
                }
                return out;
            }

            case NodeType.Block:
                return visitBlock(node);

            default:
                return '/* unknown: ' + node.type + ' */';
        }
    }

    function visitLambda(node, isVoid) {
        var params = node.params.join(', ');
        var body = visit(node.body);
        if (isVoid) {
            return 'function(' + params + ') { ' + body + '; }';
        }
        return 'function(' + params + ') { return ' + body + '; }';
    }

    function visitBlock(node) {
        if (!node) return '';
        if (node.type === NodeType.Block) {
            return node.statements.map(function(s) { return visit(s); }).join('\n');
        }
        return visit(node);
    }

    function indent(str) {
        return str.split('\n').map(function(line) { return '    ' + line; }).join('\n');
    }

    // ── Transpile: takes an array of AST nodes, returns JS string ───────
    function transpile(nodes) {
        var lines = [];
        for (var i = 0; i < nodes.length; i++) {
            lines.push(visit(nodes[i]));
        }
        return lines.join('\n');
    }

    // ── Exports ─────────────────────────────────────────────────────────
    exports.JsArrayTranspiler = {
        // AST builders
        id: id, lit: lit, member: member, index: index, call: call,
        lambda: lambda, binary: binary, unary: unary, assign: assign,
        varDecl: varDecl, stmt: stmt, ifElse: ifElse, block: block,

        // Transpiler
        visit: visit,
        transpile: transpile,

        // Maps (for inspection)
        JS_EMIT_MAP: JS_EMIT_MAP,
        JS_PROP_MAP: JS_PROP_MAP,
        JS_METHOD_MAP: JS_METHOD_MAP,
        JS_IDENT_MAP: JS_IDENT_MAP,
        OP_MAP: OP_MAP,
        VOID_LAMBDA_METHODS: VOID_LAMBDA_METHODS,
        LAMBDA_METHODS: LAMBDA_METHODS,
    };

})(typeof window !== 'undefined' ? window : module.exports);
