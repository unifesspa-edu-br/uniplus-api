using Unifesspa.UniPlus.Infrastructure.Core.Routing;

// O host exige que todo assembly com controller declare o módulo, que dá o prefixo da rota: o controller de
// AutenticacaoPorPadraoTests responde sob api/teste.
[assembly: ApiModule("teste")]
