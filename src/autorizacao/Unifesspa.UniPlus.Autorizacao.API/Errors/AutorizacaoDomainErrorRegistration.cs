namespace Unifesspa.UniPlus.Autorizacao.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, AutorizacaoDomainErrorRegistration>().")]
internal sealed class AutorizacaoDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        new(PerfilErrorCodes.NomeInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.nome_invalido", "Nome do perfil inválido")),
        new(PerfilErrorCodes.DescricaoInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.descricao_invalida", "Descrição do perfil inválida")),
        new(PerfilErrorCodes.SemPermissao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.sem_permissao", "O perfil precisa reunir ao menos uma permissão")),
        new(PerfilErrorCodes.PermissaoInexistente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.permissao_inexistente", "Permissão fora do catálogo")),
        new(PerfilErrorCodes.PermissaoReservada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.permissao_reservada", "Permissão reservada aos perfis de governança")),
        new(PerfilErrorCodes.AlcanceMisto, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.alcance_misto", "O perfil não pode misturar permissões de unidade e institucionais")),
        new(PerfilErrorCodes.AlcanceAlterado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.alcance_alterado", "O alcance de um perfil com concessões não pode mudar")),
        new(PerfilErrorCodes.AmpliacaoExigeProtecaoDeDados, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.ampliacao_exige_protecao_de_dados", "Acrescentar permissão pessoal ou sensível a perfil com concessões exige a aprovação da proteção de dados")),
        new(PerfilErrorCodes.GovernancaImutavel, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.perfil.governanca_imutavel", "Perfil de governança não pode ser alterado")),
        new(PerfilErrorCodes.NomeDuplicado, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.perfil.nome_duplicado", "Já existe perfil com este nome")),
        new(PerfilErrorCodes.IdDivergente, new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.autorizacao.perfil.id_divergente", "O identificador da URL não corresponde ao do corpo da requisição")),
        new(PerfilErrorCodes.NaoEncontrado, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.autorizacao.perfil.nao_encontrado", "Perfil de acesso não encontrado")),

        // Mesmo código do conflito que o GlobalExceptionMiddleware emite para endpoints sem
        // idempotência: a corrida é a mesma, só muda quem a traduz. Retentável, porque a
        // requisição idêntica repetida depois da corrida lê a versão nova.
        new(PerfilErrorCodes.ConflitoDeConcorrencia, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.concorrencia.conflito", "Conflito de concorrência", RetryableConflict: true)),

        new(ConcessaoErrorCodes.NaoEncontrada, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.autorizacao.concessao.nao_encontrada", "Concessão de acesso não encontrada")),
        new(ConcessaoErrorCodes.JaRevogada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.ja_revogada", "Concessão de acesso já revogada")),
        new(ConcessaoErrorCodes.MinimoDeGestores, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.minimo_de_gestores", "A revogação deixaria menos de dois gestores de acessos")),
        new(ConcessaoErrorCodes.ConfirmacaoDivergente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.confirmacao_divergente", "O e-mail informado não é o do beneficiário da concessão")),
        new(ConcessaoErrorCodes.FiltroDePessoaInvalido, new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.autorizacao.concessao.filtro_de_pessoa_invalido", "Filtro de pessoa das concessões inválido")),
        new(ConcessaoErrorCodes.FiltroDeSituacaoInvalido, new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.autorizacao.concessao.filtro_de_situacao_invalido", "Situação do filtro de concessões inválida")),
        new(ConcessaoErrorCodes.MotivoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.motivo_invalido", "Motivo da revogação inválido")),
        new(ConcessaoErrorCodes.PerfilInativo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.perfil_inativo", "Perfil de acesso desativado não aceita concessão")),
        new(ConcessaoErrorCodes.Duplicada, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.concessao.duplicada", "Já existe concessão igual não revogada, vigente ou vencida; revogue-a antes de conceder de novo")),
        new(ConcessaoErrorCodes.ValidadeInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.validade_invalida", "Fim da vigência inválido")),
        new(ConcessaoErrorCodes.EscopoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.concessao.escopo_invalido", "Escopo incoerente ou não admitido pelo perfil")),
        new(ConcessaoErrorCodes.AutorNaoIdentificado, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.autorizacao.concessao.autor_nao_identificado", "Autor da operação não identificado")),
        new(ConcessaoErrorCodes.ConflitoDeConcorrencia, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.concorrencia.conflito", "Conflito de concorrência", RetryableConflict: true)),

        new(InstalacaoErrorCodes.JaHaGestor, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.ja_ha_gestor", "Já existe gestor de acessos ativo", RecusaAposGravacao: true)),
        new(InstalacaoErrorCodes.JaHaGestorAoIniciar, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.ja_ha_gestor", "Já existe gestor de acessos ativo")),
        new(InstalacaoErrorCodes.ExecutorEntreGestores, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.executor_entre_gestores", "Quem executa a instalação não pode estar entre os gestores")),
        new(InstalacaoErrorCodes.GestoresInvalidos, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.gestores_invalidos", "Gestores da instalação inválidos")),
        new(InstalacaoErrorCodes.DocumentoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.documento_invalido", "Documento de designação inválido")),
        new(InstalacaoErrorCodes.SituacaoInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.situacao_invalida", "A instalação não está na situação que este passo exige")),
        new(InstalacaoErrorCodes.NaoEncontrada, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.autorizacao.instalacao.nao_encontrada", "Instalação não encontrada")),
        new(InstalacaoErrorCodes.EmailNaoInstitucional, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.email_nao_institucional", "O e-mail da sessão não é institucional verificado")),
        new(InstalacaoErrorCodes.CodigoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.instalacao.codigo_invalido", "Código de confirmação inválido", RecusaAposGravacao: true)),
        new(InstalacaoErrorCodes.AutorNaoIdentificado, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.autorizacao.instalacao.autor_nao_identificado", "Autor da operação não identificado")),
        new(InstalacaoErrorCodes.ConflitoDeConcorrencia, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.concorrencia.conflito", "Conflito de concorrência", RetryableConflict: true)),

        new(PedidoErrorCodes.NaoEncontrado, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.autorizacao.pedido.nao_encontrado", "Pedido de concessão não encontrado")),
        new(PedidoErrorCodes.PerfilInativo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.perfil_inativo", "Perfil de acesso desativado não aceita pedido")),
        new(PedidoErrorCodes.EscopoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.escopo_invalido", "Escopo incoerente ou não admitido pelo perfil")),
        new(PedidoErrorCodes.UnidadeInexistente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.unidade_inexistente", "A unidade do escopo não existe")),
        new(PedidoErrorCodes.JustificativaInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.justificativa_invalida", "Justificativa do pedido inválida")),
        new(PedidoErrorCodes.BeneficiarioInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.beneficiario_invalido", "Beneficiário do pedido inválido")),
        new(PedidoErrorCodes.SolicitanteNaoInstitucional, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.autorizacao.pedido.solicitante_nao_institucional", "Pedido por outra pessoa exige e-mail institucional verificado")),
        new(PedidoErrorCodes.BeneficiarioSemNome, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.beneficiario_sem_nome", "Beneficiário sem nome na sessão")),
        new(PedidoErrorCodes.PedidoDuplicado, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.pedido.pedido_duplicado", "Já existe pedido ativo igual para este beneficiário, perfil e escopo")),
        new(PedidoErrorCodes.EtapaInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.etapa_invalida", "O pedido não aguarda a etapa deste aprovador")),
        new(PedidoErrorCodes.SituacaoInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.situacao_invalida", "O pedido não está na situação que esta operação exige")),
        new(PedidoErrorCodes.DecisorEParte, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.decisor_e_parte", "Ninguém decide pedido em que é solicitante ou beneficiário")),
        new(PedidoErrorCodes.MesmoDecisorNasDuasEtapas, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.mesmo_decisor_nas_duas_etapas", "A mesma pessoa não decide as duas etapas")),
        new(PedidoErrorCodes.MotivoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.motivo_invalido", "Motivo da recusa inválido")),
        new(PedidoErrorCodes.FilaInvalida, new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.autorizacao.pedido.fila_invalida", "Fila de pedidos inválida")),
        new(PedidoErrorCodes.CursorInvalido, new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.autorizacao.pedido.cursor_invalido", "Cursor de paginação dos pedidos inválido")),
        new(PedidoErrorCodes.FiltroDeSituacaoInvalido, new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.autorizacao.pedido.filtro_de_situacao_invalido", "Situação do filtro de pedidos inválida")),
        new(PedidoErrorCodes.JaTemConcessao, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.pedido.ja_tem_concessao", "O beneficiário já tem concessão ativa igual")),
        new(PedidoErrorCodes.AutorNaoIdentificado, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.autorizacao.pedido.autor_nao_identificado", "Autor da operação não identificado")),
        new(PedidoErrorCodes.NaoEOBeneficiario, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.autorizacao.pedido.nao_e_o_beneficiario", "Só o beneficiário do pedido o aceita")),
        new(PedidoErrorCodes.PrazoVencido, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.pedido.prazo_vencido", "O prazo da concessão venceu antes da aprovação", RecusaAposGravacao: true)),
        new(PedidoErrorCodes.ValidadeInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.validade_invalida", "Fim da vigência inválido")),
        new(PedidoErrorCodes.PerfilAlterado, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.pedido.perfil_alterado", "O perfil mudou depois da aprovação do gestor", RecusaAposGravacao: true)),
        new(PedidoErrorCodes.AnexoNaoVerificado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.anexo_nao_verificado", "O anexo ainda não foi verificado")),
        new(PedidoErrorCodes.AnexoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.pedido.anexo_invalido", "Anexo do pedido inválido")),

        new(PedidoErrorCodes.SemPermissao, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.auth.forbidden", "Acesso negado")),

        // O token não traz o que identifica o solicitante: não houve decisão de acesso a tomar, e a borda responde 401.
        new(PedidoErrorCodes.IdentidadeIncompleta, new DomainErrorMapping(StatusCodes.Status401Unauthorized, "uniplus.autorizacao.pedido.identidade_incompleta", "Identidade do solicitante incompleta")),
        new(PedidoErrorCodes.ConflitoDeConcorrencia, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.concorrencia.conflito", "Conflito de concorrência", RetryableConflict: true)),
    ];
}
