import {RelayError} from './model.js';

export function botInstallUrl(clientId) {
  if(!/^\d+$/.test(clientId??''))throw new RelayError('The relay operator must configure the Discord application ID.',503);
  // Create/edit the bot's own events; view/post announcement images. No Administrator.
  const permissions=(1n<<44n)|(1n<<10n)|(1n<<11n)|(1n<<14n)|(1n<<15n);
  return 'https://discord.com/oauth2/authorize?'+new URLSearchParams({client_id:clientId,scope:'bot',permissions:permissions.toString(),integration_type:'0'});
}
