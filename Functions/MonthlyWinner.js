const https = require('https');
module.exports = async function (context, myTimer) {
    try {
        var test = new Date();

        context.log(test);

        test.setDate(test.getDate() + 1);

        if(test.getDate() === 1) {

            context.log("executing Monthly Winner");

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
        }
        else {
            context.log("not today");
        }
    } catch (error) {
        context.log(error);
    }
};
