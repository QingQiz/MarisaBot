module.exports = {
    globals: {
        defineProps: "readonly",
        defineEmits: "readonly",
        defineExpose: "readonly",
        withDefaults: "readonly"
    },
    parserOptions: {
        parser: "@typescript-eslint/parser",
        ecmaVersion: 6,
        sourceType: "module"
    },
    parser: "vue-eslint-parser",
    extends: [],
}