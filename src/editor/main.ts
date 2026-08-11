import '../content/bootstrap-default';
import '../styles/app.css';
import '../styles/editor.css';
import { EditorApp, initialLevel } from './app';

new EditorApp(initialLevel()).mount(document.getElementById('app')!);
