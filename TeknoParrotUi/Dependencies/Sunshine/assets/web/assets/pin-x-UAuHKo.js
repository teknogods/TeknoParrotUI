import{c as t,a,i as o}from"./createLucideIcon-Dqb7x8z_.js";import{N as s}from"./Navbar-15iOpcEq.js";import{a as c}from"./fetch_utils-B6IQizCo.js";import{M as p}from"./monitor-B_fKYoXp.js";import"./Notification-Cji9SyhI.js";/**
 * @license lucide-vue-next v1.0.0 - ISC
 *
 * This source code is licensed under the ISC license.
 * See the LICENSE file in the root directory of this source tree.
 */const l=t("forward",[["path",{d:"m15 17 5-5-5-5",key:"nf172w"}],["path",{d:"M4 18v-2a4 4 0 0 1 4-4h12",key:"jmiej9"}]]);/**
 * @license lucide-vue-next v1.0.0 - ISC
 *
 * This source code is licensed under the ISC license.
 * See the LICENSE file in the root directory of this source tree.
 */const u=t("hash",[["line",{x1:"4",x2:"20",y1:"9",y2:"9",key:"4lhtct"}],["line",{x1:"4",x2:"20",y1:"15",y2:"15",key:"vyu0kd"}],["line",{x1:"10",x2:"8",y1:"3",y2:"21",key:"1ggp8o"}],["line",{x1:"16",x2:"14",y1:"3",y2:"21",key:"weycgp"}]]);let y=a({components:{Navbar:s,Forward:l,Hash:u,Monitor:p},inject:["i18n"],methods:{registerDevice(m){let n=document.querySelector("#pin-input").value,i=document.querySelector("#name-input").value;document.querySelector("#status").innerHTML="";let r=JSON.stringify({pin:n,name:i});c("./api/pin",{method:"POST",headers:{"Content-Type":"application/json"},body:r}).then(e=>e.json()).then(e=>{e.status===!0?(document.querySelector("#status").innerHTML=`<div class="alert alert-success" role="alert">${this.i18n.t("pin.pair_success")}</div>`,document.querySelector("#pin-input").value="",document.querySelector("#name-input").value=""):document.querySelector("#status").innerHTML=`<div class="alert alert-danger" role="alert">${this.i18n.t("pin.pair_failure")}</div>`})}}});o(y);
