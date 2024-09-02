
const https = require('https');
module.exports = async function (context, req) {
    try {
        context.log("executing Manual  Monthly Winner");
        const options = {
                hostname: process.env["HOST_NAME"],
                port: 443,
                path: '/job/monthly?secret=' + process.env["FUNCTIONS_SECRET"],
                method: 'POST',
                headers: {
                    'Content-Length': 0
                }
            }

            var req = https.request(options, (res) => {
                res.on('data', (d) => {
                    context.log(d);
                });
            });

            req.on('error', (e) => {
                context.log(e);
            });

            req.end();
    } catch (error) {
        context.log(error);
    }
};
