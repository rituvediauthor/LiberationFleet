export type HowToBlock =
  | { type: 'paragraph'; text: string }
  | { type: 'heading'; text: string }
  | { type: 'list'; items: string[] };

export interface HowToGuideTopic {
  id: string;
  title: string;
  icon: string;
  blocks: HowToBlock[];
}

export const HOW_TO_USE_WELCOME =
  'Welcome to Community as it is meant to be.';

export const HOW_TO_USE_TOPICS: HowToGuideTopic[] = [
  {
    id: 'giving-season',
    title: 'The Giving Season',
    icon: 'fa-gift',
    blocks: [
      {
        type: 'paragraph',
        text:
          'The giving season is a community powered mechanism to both recover from financial hardships and prevent the impact of hardships from being debilitating.'
      },
      {
        type: 'paragraph',
        text:
          'There is little to gain from engaging with the Giving Season selfishly. Mechanisms are in place to make efforts to abuse the system yield very little. Meanwhile, engaging with the system genuinely will prove extremely fruitful for all involved.'
      },
      {
        type: 'paragraph',
        text:
          'Engaging with the Giving season requires very little as a bare minimum. You need only have at least one payment platform registered to your profile along with an estimated monthly contribution amount (the amount you anticipate you will be able to gift financially to the system). After this you can either join an active giving season or vote to start the first for your crew.'
      },
      {
        type: 'paragraph',
        text:
          'Once a giving season has begun, you need only click the “Next aid” widget at the top of your crew dashboard to see who is next in line to receive aid and record a gift you have already given via a third party financial app. The amount you contribute is completely up to you. Give what you can, when you can, and if you want to. That is all you need to do.'
      },
      {
        type: 'paragraph',
        text:
          'Each participant in the Giving Season will receive their own turn to get concentrated aid up to a certain amount (either fixed or based on crew contribution averages).'
      },
      {
        type: 'paragraph',
        text:
          'You may also toggle a “Survival Threshold” setting in your profile, and if your crew is set up to give survival threshold gifts, you may receive a small sum (varying in amount) to help you cover monthly expenses you may not have the funds to satisfy yourself.'
      },
      {
        type: 'paragraph',
        text:
          'There are other metrics you can adjust in your profile to impact your priority score based upon emergency circumstances or your level of vulnerability to hardship due to belonging to a minority group that is commonly targeted with hardships by people and systems.'
      }
    ]
  },
  {
    id: 'library-of-things',
    title: 'The Library of Things',
    icon: 'fa-boxes-stacked',
    blocks: [
      {
        type: 'paragraph',
        text:
          'The library of things (LoT) is not a market place despite the resemblance. LoT is a mechanism for resource sharing that helps keep funds and money within a community by enabling people to find goods and services they can access for free as opposed to spending money that will then leave the community and end up in some corporate pocket. The more money a community can save via resource sharing, the more aid they can provide to overcome hardships.'
      },
      {
        type: 'paragraph',
        text:
          'You can offer a variety of goods in the LoT be it services, durable goods, consumable goods, or digital goods.'
      },
      {
        type: 'paragraph',
        text:
          'Consumable goods, digital goods, and services are not generally things that exchange hands more than once. Therefore, the acquisition of such goods is treated more like a gift where the value of the good or service is recorded as the provider’s contribution (impacting their priority score).'
      },
      {
        type: 'paragraph',
        text:
          'Meanwhile durable goods are handled a little differently because they can exchange hands many times. Uploading a durable good to the LoT makes it a possession of the crew. Upon the first time the good is requested and delivered, the initial contributor of the good is rewarded with a recorded contribution equal to the value of the durable good along with a contribution amount equal to 10% of the value of the good for successfully delivering the good to the new possessor.'
      },
      {
        type: 'paragraph',
        text:
          'From this point of each time the durable good exchanges hands the prior possessor is rewarded with a recorded contribution equal to 10% the recorded value of the item.'
      },
      {
        type: 'paragraph',
        text:
          'Possessors can also record any amount of money they spent to maintain the durable goods. This counts as a contribution to the crew.'
      },
      {
        type: 'paragraph',
        text:
          'All recorded contributions increase your priority score, helping you receive aid sooner and giving you priority to receive a good or service when a LoT offering is in short supply.'
      }
    ]
  },
  {
    id: 'social-functions',
    title: 'Social functions',
    icon: 'fa-comments',
    blocks: [
      {
        type: 'paragraph',
        text:
          'There are four primary ways to engage socially with your crew/fleet on this app; social media feed, text based chat rooms, voice based chat rooms, and direct messages.'
      },
      {
        type: 'paragraph',
        text:
          'First is your crew/fleet’s social media feeds. Anyone in the crew can make posts to the feed and receive comments and likes in response. Use this to let your crew or fleet know what sort of things you are up to.'
      },
      {
        type: 'paragraph',
        text:
          'Next is your text based chat rooms where you can engage in conversation about various topics. Use this to engage in general or specific conversations with your crew or fleet ranging from fandom dedicated rooms to talk about your favorite entertainment to self promotion rooms to talk about projects you are working on or promote goods and services you provide.'
      },
      {
        type: 'paragraph',
        text:
          'There are also voice based chat rooms where anyone with a mic can hop on to what is essentially a crew-wide or fleet-wide phone call.'
      },
      {
        type: 'paragraph',
        text:
          'Finally, there are direct messages where you can engage in conversations in text based chatrooms that are between only you and one of your friends.'
      }
    ]
  },
  {
    id: 'democracy',
    title: 'Democracy',
    icon: 'fa-list-check',
    blocks: [
      {
        type: 'paragraph',
        text:
          'You will share possession of your crew and fleet with the others who are in it. This means that certain decisions need community approval before they are enacted. This includes any change to crew or fleet settings, kicking other users, creating and editing new rules and chatrooms, approving new crewmates to join the crew, electing users to different crew roles, and demoting crewmates from elected roles among other things.'
      },
      {
        type: 'paragraph',
        text:
          'With some exceptions, proposals are suggestions to make changes or requests for other sorts of approval upon which anyone in the crew can vote if the proposal is scoped to the crew or anyone in the fleet if scoped to the fleet (scope is just determined by the dashboard you used to make the proposal, fleet or crew).'
      },
      {
        type: 'paragraph',
        text:
          'Proposals can settle early once approve or reject votes reach a majority of eligible voters (half, rounded up). Proposals will auto-resolve after 24 hours unless approval of the proposal exceeds 50% of eligible voters at which point the proposal will immediately resolve as approved. Upon one rejection the auto-resolve timer on the proposal extends to seven days so that more people can provide their input. If rejection of the proposal exceeds 50% of the eligible voters, the proposal will immediately resolve as rejected. When the timer expires instead, whichever side has more votes wins; equal approve and reject counts follow your crew or fleet’s tied vote timeout setting.'
      },
      {
        type: 'paragraph',
        text: 'You can propose to change the auto-resolve functions in the crew/fleet settings.'
      }
    ]
  },
  {
    id: 'community-roles',
    title: 'Community roles',
    icon: 'fa-user-shield',
    blocks: [
      {
        type: 'paragraph',
        text:
          'In order to ensure crews run smoothly, crewmates can be elected to various roles with different responsibilities, most with special powers they can enact within the scope of the crew. These roles include the following:'
      },
      { type: 'heading', text: 'Advocate' },
      {
        type: 'paragraph',
        text:
          'Resolve conflict and serve as a mouthpiece for anonymous crew opinions. Can toggle anonymous mode in crew chat channels.'
      },
      { type: 'heading', text: 'Decentralizer' },
      {
        type: 'paragraph',
        text:
          'Responsible for identifying concentrations of power and decentralizing them through the creation of back-up systems, back-up records, and nominating crewmates to spread out powers. Can export the gift log and crewmate states.'
      },
      { type: 'heading', text: 'Ceremonial organizer' },
      {
        type: 'paragraph',
        text: 'Organize events, celebrations, and ceremonies for the crew. No special app powers.'
      },
      { type: 'heading', text: 'Moderator' },
      {
        type: 'paragraph',
        text: 'Delete inappropriate file attachments and restrict a crewmate\'s ability to attach files.'
      },
      { type: 'heading', text: 'Intermediary' },
      {
        type: 'paragraph',
        text:
          'Bridge gifts when the giver and recipient do not share a payment platform. Automatically loses the role after failing to complete two gifts in a row, or two gifts in the same calendar month.'
      },
      { type: 'heading', text: 'Organizer' },
      {
        type: 'paragraph',
        text:
          'Can function as a holder of any of the other roles with access to all of the powers those roles entail.'
      },
      { type: 'heading', text: 'Representative' },
      {
        type: 'paragraph',
        text:
          'Serve a fixed term receiving mutual aid (except survival thresholds) so they can take time off work to speak or vote for the crew at government functions. Nominations require a future start and end date.'
      },
      { type: 'heading', text: 'Accountant' },
      {
        type: 'paragraph',
        text:
          'Propose adjustments to crewmate contribution and reception totals, monthly giving capacity, and whether a season cycle is already completed—useful when a crew joins mid-season with existing mutual-aid history.'
      },
      {
        type: 'paragraph',
        text:
          'Crewmates who hold an elected role will always be considered a financial member in the giving season, entitling them to receive concentrated aid in the full amount when it is their turn to receive concentrated aid.'
      },
      {
        type: 'paragraph',
        text:
          'Anyone who holds a role can demote themselves without community approval and anyone else can propose demotion from a role or nomination to a role of others.'
      }
    ]
  },
  {
    id: 'profile-settings',
    title: 'My profile settings',
    icon: 'fa-user',
    blocks: [
      {
        type: 'paragraph',
        text:
          'Each crewmate can change and adjust certain parts of their profile. Apart from your standard settings, there are a number of other settings which impact your priority score.'
      },
      { type: 'heading', text: 'In need of aid' },
      {
        type: 'paragraph',
        text:
          'The “In need of aid” toggle determines if you will receive either survival threshold aid or concentrated cycle aid. This can only be toggled off if your monthly contributions exceed the in-need threshold crew setting.'
      },
      { type: 'heading', text: 'Needs survival aid' },
      {
        type: 'paragraph',
        text:
          'The “Needs survival aid” toggle determines if you will receive monthly survival threshold aid. The amount of aid received monthly is equal to the crew\'s monthly giving capacity times the modifier (default .5) in the crew settings divided by the number of crewmates who need survival aid.'
      },
      { type: 'heading', text: 'Emergency level' },
      {
        type: 'paragraph',
        text:
          'The “Emergency level” toggle indicates how close you are to debilitating hardships as the result of insufficient finances. This can range from losing access to essential needs like housing, healthcare, food, water, electricity, and the like. This value is multiplied by your crew’s total financial contributions and then added to your priority score. This ensures that no one can take priority from those experiencing emergencies through their contributions.'
      },
      { type: 'heading', text: 'Number of people represented' },
      {
        type: 'paragraph',
        text:
          'The “Number of people represented” value should be set to the number of people you help provide for including yourself, partner(s), children, pets that are expensive to care for, roommates you share resources with, and the like. A person’s priority score gets multiplied by this amount because they represent many people.'
      },
      { type: 'heading', text: 'Level of disability' },
      {
        type: 'paragraph',
        text:
          '“Level of disability” indicates the degree to which a person faces hardship and dependency due to a disability or multiple disabilities. 0 = none; 1 = minor but self-sufficient; 2 = reduced ability to provide for yourself; 3 = reliance on service workers or animals for daily life. Unless a person’s disability level is 0, their priority score will be multiplied by this number.'
      },
      { type: 'heading', text: 'Targeted minority groups' },
      {
        type: 'paragraph',
        text:
          '“Targeted Minority Groups” are more likely to be the target of discrimination and hate crimes, increasing the frequency and severity of hardships they are likely to face. Thus we must prioritize preventing and getting them out of situations of dangerous vulnerability. A person’s priority score is increased by 10% for each selected minority group.'
      }
    ]
  },
  {
    id: 'crew-settings',
    title: 'My Crew settings',
    icon: 'fa-gear',
    blocks: [
      {
        type: 'paragraph',
        text:
          'Apart from the more apparent settings you might find in the crew settings page, there are a few which are unique to the functions of this application.'
      },
      { type: 'heading', text: 'Allow survival thresholds' },
      {
        type: 'paragraph',
        text:
          'This setting determines whether or not the crew will include survival threshold amounts in the giving season. Survival thresholds renew every month, pausing any active cycle of concentrated giving to help towards the monthly needs of crewmates who have them. Once each crewmate with a survival threshold has received an amount equal to (by default) half of the crew’s monthly giving capacity divided by each crewmate who needs a survival threshold, cycle giving will resume for the month.'
      },
      { type: 'heading', text: 'Allow cross crew giving' },
      {
        type: 'paragraph',
        text:
          'This setting determines whether or not crewmates in this crew will be able to access the mutual aid functions of the fleet in order to contribute to other crews financially. If toggled off, then this crew can only give to crewmates of their same crew and not to crewmates of other crews in the fleet. However, crewmates from other crews may still be able to contribute financially to the giving season of crews that have this setting turned off (this setting only impacts the ability to give, not the ability to receive). If toggled on, then crewmates will be able to contribute financially to the crewmates of other crews.'
      },
      { type: 'heading', text: 'Require approval for crew edits' },
      {
        type: 'paragraph',
        text:
          'This setting determines whether or not changes made to the crew settings will need to go through the proposal approval process.'
      },
      { type: 'heading', text: 'Tied vote timeout' },
      {
        type: 'paragraph',
        text:
          'This setting determines what happens when a proposal’s approval timer expires with equal approve and reject vote counts (any tie, including 0–0).'
      },
      { type: 'heading', text: 'In-need threshold' },
      {
        type: 'paragraph',
        text:
          'This setting determines the average monthly (only counting the last three months) contribution amount a roleless crewmate must have in order to be able to toggle their in-need status off.'
      },
      { type: 'heading', text: 'Financial membership contribution floor' },
      {
        type: 'paragraph',
        text:
          'This setting determines the average monthly (only counting the last three months) contribution amount a roleless crewmate must have in order to be able to be counted as a member and so receive a full cycle’s worth of concentrated aid.'
      },
      { type: 'heading', text: 'Enable Library of Things' },
      {
        type: 'paragraph',
        text:
          'This toggles crewmate access to the LoT functions (See section on the LoT for more information).'
      },
      { type: 'heading', text: 'Cycle caps' },
      {
        type: 'paragraph',
        text:
          'Cycle caps are the amount of concentrated financial aid a member or non-member might receive when it is their turn to receive concentrated cycle aid. This amount, at default, is equal to the total monthly giving capacity of the crew times 2. This value can be edited using a different multiplier or even a fixed value.'
      },
      {
        type: 'paragraph',
        text:
          'For non-members the cycle cap amount is equal to the total monthly giving capacity of the crew times .25. This multiplier can also be edited or changed to a fixed amount.'
      }
    ]
  },
  {
    id: 'fleets',
    title: 'Fleets',
    icon: 'fa-ship',
    blocks: [
      {
        type: 'paragraph',
        text:
          'Where a crew can only consist, at most, of up to 50 crewmates, a fleet can consist of a limitless number of crews, the crewmates of which can interact as if they are all a part of one big crew. However, crewmates who share a crew with each other should always try to prioritize each other in terms of providing aid.'
      }
    ]
  }
];

export function getHowToTopic(id: string): HowToGuideTopic | undefined {
  return HOW_TO_USE_TOPICS.find(topic => topic.id === id);
}
