import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

//Goggle Translate rewrites text nodes in the DOM. When Angular's router
//Later tries to remove/replace those same nodes, the browser throws
//"Failed to execute 'removeChild'/'insertBefore' on 'Node'"..These patches
//make such calls no-ops instead of crashing the app

if (typeof  Node === 'function' && Node.prototype) {
  const originalRemoveChild = Node.prototype.removeChild;
  Node.prototype.removeChild = function <T extends Node>(child :T): T {
    if (child.parentNode !== this) {
      return child;
    }
    return originalRemoveChild.apply(this, [child]) as T;
  };

  const originalInsertBefore = Node.prototype.insertBefore;
  Node.prototype.insertBefore = function <T extends Node>(newNode: T, referenceNode: Node | null): T {
    if (referenceNode && referenceNode.parentNode !== this) {
      return newNode;
    }
    return originalInsertBefore.apply(this, [newNode, referenceNode]) as T;

  };
}

bootstrapApplication(App, appConfig)
  .catch((err) => console.error(err));
