function download([string]$url) {
    $dest = $url.substring($url.lastindexof('/')+1)
    if (!(test-path $dest)) {
        (new-object system.net.webclient).downloadfile($url, $dest);
    }
}
foreach ($url in (
    'http://your_url/packages/setuptools-0.6c11-py2.7.egg',
    'http://your_url/packages/python-2.7.2.msi',
	'http://your_url/packages/mongodb.zip',
	'http://your_url/packages/10gen-mms-agent-Kobojo.zip'
)) { download $url }