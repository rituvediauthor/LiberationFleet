/**
 * Fixed App Review / demo accounts.
 * Recovery phrases use BIP39 words only (app validates wordlist membership, not checksum).
 * Password is the same across accounts so reviewers can switch users quickly.
 */

export const SHARED_PASSWORD = 'ReviewDemo!Harbor26';

/** @typedef {{ key: string, username: string, email: string, displayName: string, recoveryPhrase: string, emergencyLevel: number, disabilityLevel: number, peopleRepresented: number, inNeedOfAid: boolean, needsSurvivalAid: boolean, identityGroups: string[], venmo: string, crew: 'cedar' | 'river', role: 'captain' | 'mate' }} DemoAccount */

/** @type {DemoAccount[]} */
export const ACCOUNTS = [
  {
    key: 'maya',
    username: 'mayachen',
    email: 'maya.chen@lf-review.test',
    displayName: 'Maya Chen',
    recoveryPhrase: 'abandon ability able about above absent absorb abstract absurd abuse access accident',
    emergencyLevel: 1,
    disabilityLevel: 0,
    peopleRepresented: 2,
    inNeedOfAid: false,
    needsSurvivalAid: false,
    identityGroups: [],
    venmo: '@maya-cedar',
    crew: 'cedar',
    role: 'captain'
  },
  {
    key: 'jordan',
    username: 'jordanokonkwo',
    email: 'jordan.okonkwo@lf-review.test',
    displayName: 'Jordan Okonkwo',
    recoveryPhrase: 'account accuse achieve acid acoustic acquire across act action actor actress actual',
    emergencyLevel: 0,
    disabilityLevel: 1,
    peopleRepresented: 1,
    inNeedOfAid: false,
    needsSurvivalAid: false,
    identityGroups: ['Bipoc'],
    venmo: '@jordan-cedar',
    crew: 'cedar',
    role: 'mate'
  },
  {
    key: 'sam',
    username: 'samrivera',
    email: 'sam.rivera@lf-review.test',
    displayName: 'Sam Rivera',
    recoveryPhrase: 'adapt add addict address adjust admit adult advance advice aerobic affair afford',
    emergencyLevel: 2,
    disabilityLevel: 0,
    peopleRepresented: 3,
    inNeedOfAid: true,
    needsSurvivalAid: false,
    identityGroups: ['Bipoc', 'ImmigrantOrRefugee'],
    venmo: '@sam-cedar',
    crew: 'cedar',
    role: 'mate'
  },
  {
    key: 'priya',
    username: 'priyanair',
    email: 'priya.nair@lf-review.test',
    displayName: 'Priya Nair',
    recoveryPhrase: 'afraid again age agent agree ahead aim air airport aisle alarm album',
    emergencyLevel: 0,
    disabilityLevel: 0,
    peopleRepresented: 1,
    inNeedOfAid: false,
    needsSurvivalAid: false,
    identityGroups: ['Bipoc', 'Woman'],
    venmo: '@priya-cedar',
    crew: 'cedar',
    role: 'mate'
  },
  {
    key: 'lee',
    username: 'leepark',
    email: 'lee.park@lf-review.test',
    displayName: 'Lee Park',
    recoveryPhrase: 'alcohol alert alien all alley allow almost alone alpha already also alter',
    emergencyLevel: 3,
    disabilityLevel: 2,
    peopleRepresented: 1,
    inNeedOfAid: true,
    needsSurvivalAid: true,
    identityGroups: ['Bipoc', 'PhysicallyDisabledOrUnaccommodated'],
    venmo: '@lee-cedar',
    crew: 'cedar',
    role: 'mate'
  },
  {
    key: 'avery',
    username: 'averybrooks',
    email: 'avery.brooks@lf-review.test',
    displayName: 'Avery Brooks',
    recoveryPhrase: 'always amateur amazing among amount amused analyst anchor ancient anger angle angry',
    emergencyLevel: 1,
    disabilityLevel: 0,
    peopleRepresented: 2,
    inNeedOfAid: false,
    needsSurvivalAid: false,
    identityGroups: [],
    venmo: '@avery-river',
    crew: 'river',
    role: 'captain'
  },
  {
    key: 'casey',
    username: 'caseynguyen',
    email: 'casey.nguyen@lf-review.test',
    displayName: 'Casey Nguyen',
    recoveryPhrase: 'animal ankle announce annual another answer antenna antique anxiety any apart apology',
    emergencyLevel: 0,
    disabilityLevel: 0,
    peopleRepresented: 1,
    inNeedOfAid: false,
    needsSurvivalAid: false,
    identityGroups: ['Bipoc', 'ImmigrantOrRefugee'],
    venmo: '@casey-river',
    crew: 'river',
    role: 'mate'
  },
  {
    key: 'morgan',
    username: 'morganellis',
    email: 'morgan.ellis@lf-review.test',
    displayName: 'Morgan Ellis',
    recoveryPhrase: 'appear apple approve april arch arctic area arena argue arm armed armor',
    emergencyLevel: 2,
    disabilityLevel: 1,
    peopleRepresented: 1,
    inNeedOfAid: true,
    needsSurvivalAid: false,
    identityGroups: ['NotHeterosexual', 'Neurodivergent'],
    venmo: '@morgan-river',
    crew: 'river',
    role: 'mate'
  },
  {
    key: 'devon',
    username: 'devonwalsh',
    email: 'devon.walsh@lf-review.test',
    displayName: 'Devon Walsh',
    recoveryPhrase: 'army around arrange arrest arrive arrow art artefact artist artwork ask aspect',
    emergencyLevel: 0,
    disabilityLevel: 0,
    peopleRepresented: 4,
    inNeedOfAid: false,
    needsSurvivalAid: false,
    identityGroups: ['Woman'],
    venmo: '@devon-river',
    crew: 'river',
    role: 'mate'
  }
];

export const ORGS = {
  cedar: {
    name: 'Cedar Circle',
    privacy: 'Private',
    scope: 'Online',
    maxSize: 12
  },
  river: {
    name: 'River House',
    privacy: 'Private',
    scope: 'Online',
    maxSize: 12
  },
  fleet: {
    name: 'Harbor Mutual Aid',
    privacy: 'Private',
    scope: 'Online'
  }
};

/** Primary Apple App Review login */
export const REVIEWER_PRIMARY = 'maya';
