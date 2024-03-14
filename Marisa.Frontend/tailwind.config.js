/** @type {import('tailwindcss').Config} */
module.exports = {
    darkMode: 'meida', // or 'media' or 'class'
    theme: {
        extend: {},
        fontFamily: {
            'osu-web': 'Torus,Inter,"Helvetica Neue",Tahoma,Arial,"Hiragino Kaku Gothic ProN",Meiryo,"Microsoft YaHei","Apple SD Gothic Neo",sans-serif'.split(','),
            'fangSong': 'Torus,FangSong'.split(','),
            'osu-rank': 'Venera',
            'console': 'Consolas,monospace',
        }
    },
    variants: {
        extend: {},
    },
    plugins: [],
    content: ['./public/**/*.html', './src/**/*.{vue,js,ts,jsx,tsx}'],
}
