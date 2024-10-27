function DownloadFile(filename, contentType, content) {
    //Create the URL.
    const file = new File([content], filename, { type: contentType });
    const exportUrl = URL.createObjectURL(file);
    //Creates <a> element and clicks on it programmatically.
    const a = document.createElement("a");
    document.body.appendChild(a);
    a.href = exportUrl;
    a.download = filename;
    a.target = "_self";
    a.click();
    //Remove URL object after clicking on it.
    URL.revokeObjectURL(exportUrl);
}

function CopyToClipboard(text) {
    navigator.clipboard.writeText(text).then(function () {
        console.log('Text copied to clipboard');
    }, function (err) {
        console.error('Could not copy text: ', err);
    });
};