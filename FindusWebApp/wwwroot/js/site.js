// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

function getQueryParameters(str) {
  urlParts = str.split("?");
  if (urlParts.length < 1 || urlParts[1] == null) {
    return null;
  }
  str = urlParts[1];
  return str
    .replace(/(^\?)/, "")
    .split("&")
    .map(
      function (n) {
        return (n = n.split("=")), (this[n[0]] = n[1]), this;
      }.bind({})
    )[0];
}
function updateUrlParam(url, param, value) {

    var index = url.indexOf("?");

    if (index > 0) {

        var u = url.substring(index + 1).split("&");

        var params = new Array(u.length);

        var p;
        var found = false;

        for (var i = 0; i < u.length; i++) {
            params[i] = u[i].split("=");
            if (params[i][0] === param) {
                params[i][1] = value;
                found = true;
            }
        }

        if (!found) {
            params.push(new Array(2));
            params[params.length - 1][0] = param;
            params[params.length - 1][1] = value;
        }

        var res = url.substring(0, index + 1) + params[0][0] + "=" + params[0][1];
        for (var i = 1; i < params.length; i++) {
            res += "&" + params[i][0] + "=" + params[i][1];
        }
        return res;

    } else {
        return url + "?" + param + "=" + value;
    }

}
function replaceQueryParam(param, newval, url) {
 
  var regex = new RegExp("([?;&])" + param + "[^&;]*[;&]?");
  var query = url.replace(regex, "$1").replace(/&$/, "");
  
  /*
  if(query == url && url.indexOf("?" == -1)) {
    query += "?";
  }*/

  return (
    (query.length > 2 ? query + "&" : "") +
    (newval ? param + "=" + newval : "")
  );
}
