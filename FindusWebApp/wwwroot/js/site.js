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

function replaceQueryParam(param, newval, url) {
  var regex = new RegExp("([?;&])" + param + "[^&;]*[;&]?");
  var query = url.replace(regex, "$1").replace(/&$/, "");

  if(query == url && url.indexOf("?" == -1)) {
    query += "?";
  }

  return (
    (query.length > 2 ? query + "&" : "") +
    (newval ? param + "=" + newval : "")
  );
}
