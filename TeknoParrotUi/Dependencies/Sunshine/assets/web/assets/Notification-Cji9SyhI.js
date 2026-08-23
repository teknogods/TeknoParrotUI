import{c as d,d as c,F as u,l as m,k as f,I as k,o as a,z as g,p as h,H as _,e as l,t as p}from"./createLucideIcon-Dqb7x8z_.js";/**
 * @license lucide-vue-next v1.0.0 - ISC
 *
 * This source code is licensed under the ISC license.
 * See the LICENSE file in the root directory of this source tree.
 */const C=d("circle-alert",[["circle",{cx:"12",cy:"12",r:"10",key:"1mglay"}],["line",{x1:"12",x2:"12",y1:"8",y2:"12",key:"1pkeuh"}],["line",{x1:"12",x2:"12.01",y1:"16",y2:"16",key:"4dfq90"}]]);/**
 * @license lucide-vue-next v1.0.0 - ISC
 *
 * This source code is licensed under the ISC license.
 * See the LICENSE file in the root directory of this source tree.
 */const x=d("circle-check-big",[["path",{d:"M21.801 10A10 10 0 1 1 17 3.335",key:"yps3ct"}],["path",{d:"m9 11 3 3L22 4",key:"1pflzl"}]]);/**
 * @license lucide-vue-next v1.0.0 - ISC
 *
 * This source code is licensed under the ISC license.
 * See the LICENSE file in the root directory of this source tree.
 */const v=d("info",[["circle",{cx:"12",cy:"12",r:"10",key:"1mglay"}],["path",{d:"M12 16v-4",key:"1dtifu"}],["path",{d:"M12 8h.01",key:"e9boi3"}]]);/**
 * @license lucide-vue-next v1.0.0 - ISC
 *
 * This source code is licensed under the ISC license.
 * See the LICENSE file in the root directory of this source tree.
 */const I=d("triangle-alert",[["path",{d:"m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3",key:"wmoenq"}],["path",{d:"M12 9v4",key:"juzpu7"}],["path",{d:"M12 17h.01",key:"p32p05"}]]),b=(e,t)=>{const s=e.__vccOpts||e;for(const[n,y]of t)s[n]=y;return s},o=k({notifications:[],_nextId:1});function r(e,t,s){o.notifications.push({id:o._nextId++,type:e,message:null,title:null,messageKey:t,titleKey:s||null})}function A(e){return{danger:"AlertCircle",warning:"AlertTriangle",success:"CheckCircle",info:"Info"}[e]||"Info"}const L={error:(e,t)=>r("danger",e,t),warning:(e,t)=>r("warning",e,t),success:(e,t)=>r("success",e,t),info:(e,t)=>r("info",e,t)},K={components:{AlertCircle:C,AlertTriangle:I,CheckCircle:x,Info:v},setup(){function e(t){const s=o.notifications.findIndex(n=>n.id===t);s!==-1&&o.notifications.splice(s,1)}return{state:o,dismiss:e,iconFor:A}}},w={key:0,class:"notification-container"},z={class:"flex-grow-1"},B={key:0},M=["aria-label","onClick"];function F(e,t,s,n,y,N){return n.state.notifications.length>0?(a(),c("div",w,[(a(!0),c(u,null,m(n.state.notifications,i=>(a(),c("div",{key:i.id,class:g(["alert d-flex align-items-start gap-2 mb-2","alert-"+i.type]),role:"alert"},[(a(),h(_(n.iconFor(i.type)),{size:18,class:"icon flex-shrink-0 mt-1"})),l("div",z,[i.titleKey||i.title?(a(),c("div",B,[l("strong",null,p(i.titleKey?e.$t(i.titleKey):i.title),1)])):f("",!0),l("span",null,p(i.messageKey?e.$t(i.messageKey):i.message),1)]),l("button",{type:"button",class:"btn-close","aria-label":e.$t("_common.dismiss"),onClick:T=>n.dismiss(i.id)},null,8,M)],2))),128))])):f("",!0)}const q=b(K,[["render",F],["__scopeId","data-v-1f0290df"]]);export{C,v as I,q as N,I as T,b as _,x as a,L as n};
